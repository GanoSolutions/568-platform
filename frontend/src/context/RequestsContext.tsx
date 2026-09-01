import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useAuth } from '@/context/AuthContext';
import { swapRequestApi, SwapRequestStatus, type SwapRequestDTO, type SwapRequestStatusDTO } from '@/lib/apiClient';
import { getAppHubConnection, resetIntentionalStop, signalR, wasConnectionStoppedIntentionally } from '@/lib/realtime';
import { showErrorToast, showSuccessToast } from '@/lib/toast';
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

	// Stesso motivo: l'handler SwapRequestsChanged sotto è registrato una sola volta
	// per tutta la vita del provider, quindi leggerebbe sempre lo `user` del primo
	// render se non passasse da un ref.
	const userRef = useRef(user);
	useEffect(() => {
		userRef.current = user;
	}, [user]);

	// Segnala all'utente quando gli aggiornamenti live non sono disponibili (review
	// Hermann su PR #49): un toast per "giù" e uno per "ripristinata", senza doppioni
	// se lo stato non cambia — es. onreconnecting seguito da un onreconnected quasi
	// immediato non deve mostrare "giù" se non l'abbiamo già mostrato. Niente toast
	// se l'utente ha appena fatto logout: wasConnectionStoppedIntentionally() (da
	// realtime.ts) è impostato in modo sincrono al logout, prima ancora che
	// connection.stop() chiuda davvero il socket — a differenza di un flag derivato
	// da `user`, non rischia di arrivare in ritardo rispetto all'evento onclose.
	const liveDownRef = useRef(false);
	const markConnectionDown = useCallback(() => {
		if (wasConnectionStoppedIntentionally() || liveDownRef.current) return;
		liveDownRef.current = true;
		showErrorToast('Aggiornamenti in tempo reale non disponibili, nuovo tentativo in corso...');
	}, []);
	const markConnectionRestored = useCallback(() => {
		if (!liveDownRef.current) return;
		liveDownRef.current = false;
		showSuccessToast('Connessione in tempo reale ripristinata');
	}, []);

	// tryConnect vive dentro l'effect sotto (dipende da `user`/`authLoading`): questo
	// ref lascia onclose richiamare sempre la versione più recente senza doverla
	// ridefinire qui o aggiungere onclose alle dipendenze di quell'effect.
	const tryConnectRef = useRef<() => void>(() => {});

	// Canale live: la connessione è condivisa con useCalendar (per ShiftsChanged),
	// ma è questo provider — montato una volta sola a livello top-level — a possederla
	// (avvio/stop). Gli handler vengono registrati una sola volta per tutta la vita
	// del provider: SignalR non espone un modo per rimuovere i singoli handler di
	// onreconnected/onreconnecting/onclose, quindi se questo effect rirunasse ad ogni
	// cambio di `user` (come succedeva prima) si accumulerebbero handler duplicati ad
	// ogni riconnessione.
	useEffect(() => {
		const connection = getAppHubConnection();
		connection.on('SwapRequestsChanged', (payload: { requesterId: string; status: SwapRequestStatusDTO; shiftDate: string }) => {
			reloadRequestsRef.current({ silent: true });

			// Esito per chi ha fatto la richiesta (review Hermann su PR #49): l'evento
			// arriva a tutti, ma il toast ha senso solo per il richiedente, e solo per un
			// esito vero e proprio — non per la creazione (pending) o per un annullamento
			// fatto da lui stesso (già sa cosa ha fatto).
			if (userRef.current && payload.requesterId === userRef.current.id) {
				const [y, m, d] = payload.shiftDate.split('-');
				const dateLabel = `${d}/${m}/${y}`;
				const status = toFrontendStatus(payload.status);
				if (status === 'accepted') {
					showSuccessToast(`La tua richiesta di cambio turno del ${dateLabel} è stata accettata`);
				} else if (status === 'rejected') {
					showErrorToast(`La tua richiesta di cambio turno del ${dateLabel} è stata rifiutata`);
				}
			}
		});
		connection.onreconnected(() => {
			reloadRequestsRef.current({ silent: true });
			markConnectionRestored();
		});
		// onreconnecting: la connessione era su, è appena caduta, withAutomaticReconnect
		// (realtime.ts) sta già ritentando da solo. onclose: quei tentativi automatici si
		// sono esauriti (rimane Disconnected per sempre) — riprendiamo noi con lo stesso
		// backoff usato al primo avvio, altrimenti nessuno riconnette mai più.
		connection.onreconnecting(() => {
			markConnectionDown();
		});
		connection.onclose(() => {
			markConnectionDown();
			if (!wasConnectionStoppedIntentionally()) tryConnectRef.current();
		});

		return () => {
			connection.off('SwapRequestsChanged');
		};
	}, [markConnectionDown, markConnectionRestored]);

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
			resetIntentionalStop();
			connection.start().then(markConnectionRestored).catch(() => {
				if (cancelled) return;
				markConnectionDown();
				attempt += 1;
				const delay = Math.min(1000 * 2 ** attempt, 30000);
				retryTimer = setTimeout(tryConnect, delay);
			});
		};
		tryConnectRef.current = tryConnect;
		tryConnect();

		return () => {
			cancelled = true;
			clearTimeout(retryTimer);
		};
	}, [authLoading, user, markConnectionDown, markConnectionRestored]);

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
