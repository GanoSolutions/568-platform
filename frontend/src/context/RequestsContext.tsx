import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useAuth } from '@/context/AuthContext';
import { swapRequestApi, SwapRequestStatus, type SwapRequestDTO, type SwapRequestStatusDTO } from '@/lib/apiClient';
import { getAppHubConnection, signalR } from '@/lib/realtime';
import type { SwapRequest } from '@/types';

interface RequestsContextValue {
	requests: SwapRequest[]
	loading: boolean
	error: string
	pendingCount: number
	pendingShiftIds: Set<string>
	reloadRequests: () => Promise<void>
	createSwapRequest: (params: { shiftId: string; targetEmployeeIds: string[] }) => Promise<void>
	respondToSwapRequest: (requestId: string, decision: string) => Promise<void>
	cancelSwapRequest: (requestId: string) => Promise<void>
}

const RequestsContext = createContext<RequestsContextValue | null>(null);

function toFrontendStatus(status: SwapRequestStatusDTO): SwapRequest['status'] {
	switch (status) {
	case SwapRequestStatus.Pending: return 'pending';
	case SwapRequestStatus.Accepted: return 'accepted';
	case SwapRequestStatus.Rejected: return 'rejected';
	case SwapRequestStatus.Cancelled: return 'cancelled';
	}
}

function fromDTO(dto: SwapRequestDTO): SwapRequest {
	return {
		id: dto.id,
		shiftId: dto.shift.id,
		requesterId: dto.requesterId,
		targetEmployeeId: dto.targetEmployeeId,
		status: toFrontendStatus(dto.status),
		createdAt: dto.createdAt,
		respondedAt: dto.respondedAt,
		workDate: dto.shift.date,
		shiftUpdatedAt: dto.shift.updatedAt,
	};
}

export function RequestsProvider({ children }: { children: ReactNode }) {
	const { user, loading: authLoading } = useAuth();
	const [requests, setRequests] = useState<SwapRequest[]>([]);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState('');

	const reloadRequests = useCallback(async ({ silent = false }: { silent?: boolean } = {}) => {
		if (!user) return;

		if (!silent) setLoading(true);
		setError('');

		try {
			const dtos = await swapRequestApi.getPending();
			const sorted = dtos.map(fromDTO).sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
			setRequests(sorted);
		} catch (loadError) {
			setError(loadError instanceof Error ? loadError.message : 'Caricamento richieste non riuscito');
		} finally {
			if (!silent) setLoading(false);
		}
	}, [user]);

	useEffect(() => {
		if (authLoading || !user) return;
		reloadRequests();
	}, [authLoading, user, reloadRequests]);

	// reloadRequests cambia riferimento ad ogni cambio di `user` (es. refreshProfile()):
	// tenerlo in un ref lascia gli handler sotto stabili senza doverli ri-registrare.
	const reloadRequestsRef = useRef(reloadRequests);
	useEffect(() => {
		reloadRequestsRef.current = reloadRequests;
	}, [reloadRequests]);

	// Canale live: la connessione è condivisa con useCalendar (per ShiftsChanged),
	// ma è questo provider — montato una volta sola a livello top-level — a possederla
	// (avvio/stop). Gli handler vengono registrati una sola volta per tutta la vita
	// del provider: SignalR non espone un modo per rimuovere i singoli handler di
	// onreconnected, quindi se questo effect rirunasse ad ogni cambio di `user` (come
	// succedeva prima) si accumulerebbero handler duplicati ad ogni riconnessione.
	useEffect(() => {
		const connection = getAppHubConnection();
		connection.on('SwapRequestsChanged', () => {
			reloadRequestsRef.current({ silent: true });
		});
		connection.onreconnected(() => {
			reloadRequestsRef.current({ silent: true });
		});

		return () => {
			connection.off('SwapRequestsChanged');
		};
	}, []);

	// Avvio della connessione quando l'utente è autenticato. withAutomaticReconnect
	// copre solo le cadute dopo una connessione riuscita, non un .start() iniziale
	// fallito: qui si ritenta con backoff esponenziale finché non si connette (o finché
	// il provider non viene smontato/l'utente non fa logout).
	useEffect(() => {
		if (authLoading || !user) return;

		const connection = getAppHubConnection();
		let cancelled = false;
		let attempt = 0;
		let retryTimer: ReturnType<typeof setTimeout>;

		const tryConnect = () => {
			if (cancelled || connection.state !== signalR.HubConnectionState.Disconnected) return;
			connection.start().catch(() => {
				if (cancelled) return;
				attempt += 1;
				const delay = Math.min(1000 * 2 ** attempt, 30000);
				retryTimer = setTimeout(tryConnect, delay);
			});
		};
		tryConnect();

		return () => {
			cancelled = true;
			clearTimeout(retryTimer);
		};
	}, [authLoading, user]);

	// Le azioni sotto NON toccano `error`: quello è riservato ai fallimenti di
	// reloadRequests (background/silenzioso, mostrato come toast). Gli errori
	// di un'azione vengono solo rilanciati: il chiamante li mostra come banner
	// nella propria pagina. Farlo qui duplicava banner+toast per lo stesso errore.
	const createSwapRequest = async ({ shiftId, targetEmployeeIds }: { shiftId: string; targetEmployeeIds: string[] }) => {
		await swapRequestApi.create({ shiftId, targetEmployeeIds });
		await reloadRequests();
	};

	const respondToSwapRequest = async (requestId: string, decision: string) => {
		if (decision === 'accepted') {
			await swapRequestApi.accept(requestId);
		} else {
			await swapRequestApi.reject(requestId);
		}
		await reloadRequests();
	};

	const cancelSwapRequest = async (requestId: string) => {
		await swapRequestApi.cancel(requestId);
		await reloadRequests();
	};

	const pendingCount = useMemo(() => {
		if (!user) return 0;

		if (user.role === 'admin') {
			return requests.filter((request) => request.status === 'pending').length;
		}

		return requests.filter(
			(request) => request.status === 'pending' && request.targetEmployeeId === user.id,
		).length;
	}, [requests, user]);

	const pendingShiftIds = useMemo(
		() => new Set(requests.filter((request) => request.status === 'pending').map((request) => request.shiftId)),
		[requests],
	);

	return (
		<RequestsContext.Provider
			value={{
				requests,
				loading,
				error,
				pendingCount,
				pendingShiftIds,
				reloadRequests,
				createSwapRequest,
				respondToSwapRequest,
				cancelSwapRequest,
			}}
		>
			{children}
		</RequestsContext.Provider>
	);
}

// eslint-disable-next-line react-refresh/only-export-components
export function useRequests(): RequestsContextValue {
	const ctx = useContext(RequestsContext);
	if (!ctx) throw new Error('useRequests must be used within RequestsProvider');
	return ctx;
}
