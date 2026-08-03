import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { useAuth } from '@/context/AuthContext';
import { swapRequestApi, SwapRequestStatus, type SwapRequestDTO, type SwapRequestStatusDTO } from '@/lib/apiClient';
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

/**
 * Stopgap: il backend non ha (ancora) un canale realtime/websocket per le
 * swap request, quindi la lista viene ricaricata a intervalli mentre si è
 * sulla pagina Richieste. Da sostituire quando arriva un vero canale live
 * (vedi issue #47).
 */
const POLL_INTERVAL_MS = 5000;
const REQUESTS_PAGE_PATH = '/requests';

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
	const location = useLocation();
	const isOnRequestsPage = location.pathname === REQUESTS_PAGE_PATH;
	const [requests, setRequests] = useState<SwapRequest[]>([]);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState('');

	const reloadRequests = useCallback(async ({ silent = false }: { silent?: boolean } = {}) => {
		if (!user) return;

		if (!silent) setLoading(true);
		setError('');

		try {
			const dtos = await swapRequestApi.getForUser();
			setRequests(dtos.map(fromDTO));
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

	useEffect(() => {
		if (authLoading || !user || !isOnRequestsPage) return;

		const intervalId = window.setInterval(() => reloadRequests({ silent: true }), POLL_INTERVAL_MS);
		return () => window.clearInterval(intervalId);
	}, [authLoading, user, isOnRequestsPage, reloadRequests]);

	const createSwapRequest = async ({ shiftId, targetEmployeeIds }: { shiftId: string; targetEmployeeIds: string[] }) => {
		setError('');
		try {
			await swapRequestApi.create({ shiftId, targetEmployeeIds });
		} catch (createError) {
			setError(createError instanceof Error ? createError.message : 'Richiesta cambio turno non riuscita');
			throw createError;
		}
		await reloadRequests();
	};

	const respondToSwapRequest = async (requestId: string, decision: string) => {
		setError('');
		try {
			if (decision === 'accepted') {
				await swapRequestApi.accept(requestId);
			} else {
				await swapRequestApi.reject(requestId);
			}
		} catch (respondError) {
			setError(respondError instanceof Error ? respondError.message : 'Aggiornamento richiesta non riuscito');
			throw respondError;
		}
		await reloadRequests();
	};

	const cancelSwapRequest = async (requestId: string) => {
		setError('');
		try {
			await swapRequestApi.cancel(requestId);
		} catch (cancelError) {
			setError(cancelError instanceof Error ? cancelError.message : 'Annullamento richiesta non riuscito');
			throw cancelError;
		}
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
