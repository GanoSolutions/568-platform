/**
 * HTTP client for the C# backend.
 *
 * - Adds `Authorization: Bearer <token>` automatically from localStorage.
 * - On 401, attempts a single silent token refresh and retries the request.
 * - On refresh failure, clears tokens and dispatches `app:logout`.
 * - Throws `ApiError` for any non-2xx response.
 */
import { jwtDecode } from 'jwt-decode';

export interface JwtPayload {
	/** Standard nameid claim — set to user GUID by .NET JwtService */
	nameid?: string;
	/** .NET maps ClaimTypes.Email here */
	email?: string;
	/** Expiration time as a Unix timestamp in seconds */
	exp?: number;
}

// ---------------------------------------------------------------------------
// Backend DTO types (mirror of C# models — enums are numeric by default)
// ---------------------------------------------------------------------------

/** Numeric values match the C# UserRole enum: Admin=0, Manager=1, Employee=2 */
export type UserRoleCode = 0 | 1 | 2;

/** Numeric values match the C# UserStatus enum: Pending=0, Invited=1, Active=2, Disabled=3 */
export type UserStatusCode = 0 | 1 | 2 | 3;

/**
 * Mirror of the C# UserStatus enum (serialized as numbers).
 * - Pending:  creato, invito non ancora inviato
 * - Invited:  invito inviato, primo accesso non completato
 * - Active:   primo accesso completato
 * - Disabled: account disabilitato
 */
export const UserStatus = {
	Pending: 0,
	Invited: 1,
	Active: 2,
	Disabled: 3,
} as const;

export interface Tokens {
	accessToken: string;
	refreshToken: string;
}

/** Mirror of the C# EmployeeDTO — dati anagrafici del dipendente. */
export interface EmployeeDTO {
	name: string;
	surname: string;
	fiscalCode: string;
	phone: string;
	color: string | null;
	/** DateOnly serializzato come "YYYY-MM-DD", null se a tempo indeterminato */
	contractEnd: string | null;
}

export interface UserDTO {
	id: string;
	email: string;
	role: UserRoleCode;
	status: UserStatusCode;
	/** Presente solo dopo che l'utente ha accettato l'invito, altrimenti null */
	employee: EmployeeDTO | null;
	createdAt: string;
}

export interface UserRegisterPayload {
	email: string;
	fullName: string;
	password: string;
	role: UserRoleCode;
}

/** Mirror of the C# ShiftDTO. `startTime` is "HH:mm:ss", `duration` is a .NET
 * TimeSpan string "[d.]hh:mm:ss" (e.g. "06:00:00", or "1.00:00:00" for 24h). */
export interface ShiftDTO {
	id: string;
	date: string;
	employeeId: string;
	startTime: string;
	duration: string;
	createdBy: string;
	createdAt: string;
}

export interface ShiftCreatePayload {
	employeeId: string;
	date: string;
	startTime: string;
	duration: string;
}

export interface ShiftUpdatePayload {
	startTime: string;
	duration: string;
}

export interface ChangePasswordPayload {
	currentPassword: string;
	newPassword: string;
}

// ---------------------------------------------------------------------------
// Token storage
// ---------------------------------------------------------------------------

const ACCESS_TOKEN_KEY = 'api_access_token';
const REFRESH_TOKEN_KEY = 'api_refresh_token';

/**
 * Decodes the JWT payload and returns the user ID.
 * Returns null if the token is missing, malformed, or has no `nameid`.
 */
export function getUserIdFromToken(token: string): string | null {
	try {
		const payload = jwtDecode<JwtPayload>(token);
		return typeof payload.nameid === 'string' ? payload.nameid : null;
	} catch {
		return null;
	}
}

/**
 * How many seconds before the actual expiry a token is already considered
 * "expiring" — gives a small margin to refresh proactively and avoid sending
 * a request with a token that expires mid-flight.
 */
const TOKEN_EXPIRY_WINDOW_SECONDS = 5;

/**
 * Returns true if the access token is expired or will expire within the next
 * {@link TOKEN_EXPIRY_WINDOW_SECONDS} seconds.
 *
 * Returns false for tokens without an `exp` claim or that can't be decoded:
 * in those cases we let the request proceed and rely on the 401 fallback.
 */
export function isTokenExpiringSoon(token: string): boolean {
	try {
		const { exp } = jwtDecode<JwtPayload>(token);
		if (typeof exp !== 'number') return false;
		const nowSeconds = Date.now() / 1000;
		return exp - nowSeconds <= TOKEN_EXPIRY_WINDOW_SECONDS;
	} catch {
		return false;
	}
}

export function getAccessToken(): string | null {
	return localStorage.getItem(ACCESS_TOKEN_KEY);
}

export function getRefreshToken(): string | null {
	return localStorage.getItem(REFRESH_TOKEN_KEY);
}

export function setTokens(tokens: Tokens): void {
	localStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken);
	localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
}

export function clearTokens(): void {
	localStorage.removeItem(ACCESS_TOKEN_KEY);
	localStorage.removeItem(REFRESH_TOKEN_KEY);
}

// ---------------------------------------------------------------------------
// Base URL
// ---------------------------------------------------------------------------

const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Error type
// ---------------------------------------------------------------------------

export class ApiError extends Error {
	constructor(
		public readonly status: number,
		message: string,
	) {
		super(message);
		this.name = 'ApiError';
	}
}

// ---------------------------------------------------------------------------
// Token refresh (singleton promise to avoid parallel refresh calls)
// ---------------------------------------------------------------------------

let pendingRefresh: Promise<Tokens> | null = null;

async function doRefresh(): Promise<Tokens> {
	const accessToken = getAccessToken();
	const refreshToken = getRefreshToken();

	if (!accessToken || !refreshToken) {
		throw new ApiError(401, 'No tokens available');
	}

	const res = await fetch(`${API_BASE}/auth/refresh`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify({ accessToken, refreshToken }),
	});

	if (!res.ok) {
		clearTokens();
		throw new ApiError(401, 'Session expired');
	}

	const tokens = (await res.json()) as Tokens;
	setTokens(tokens);
	return tokens;
}

function refreshOnce(): Promise<Tokens> {
	if (!pendingRefresh) {
		pendingRefresh = doRefresh().finally(() => {
			pendingRefresh = null;
		});
	}
	return pendingRefresh;
}

// ---------------------------------------------------------------------------
// Core request function
// ---------------------------------------------------------------------------

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
	const headers: Record<string, string> = {
		'Content-Type': 'application/json',
		...(init.headers as Record<string, string> | undefined),
	};

	let token = getAccessToken();

	// Proactive refresh: if the access token is already expired (or about to be),
	// refresh once *before* sending the request so we don't waste a round-trip on
	// a guaranteed 401. The reactive 401 handler below still covers edge cases
	// (server-side revocation, clock skew) where the token looks valid but isn't.
	if (token && getRefreshToken() && isTokenExpiringSoon(token)) {
		try {
			token = (await refreshOnce()).accessToken;
		} catch {
			// Proactive refresh failed — let the request go out and the 401 path
			// handle the logout, so behaviour matches the reactive case.
		}
	}

	if (token) {
		headers['Authorization'] = `Bearer ${token}`;
	}

	const response = await fetch(`${API_BASE}${path}`, { ...init, headers });

	if (response.status === 401) {
		try {
			const newTokens = await refreshOnce();
			headers['Authorization'] = `Bearer ${newTokens.accessToken}`;

			const retried = await fetch(`${API_BASE}${path}`, { ...init, headers });

			if (!retried.ok) {
				throw new ApiError(retried.status, await extractErrorMessage(retried));
			}

			return parseBody<T>(retried);
		} catch (err) {
			clearTokens();
			window.dispatchEvent(new Event('app:logout'));
			if (err instanceof ApiError) throw err;
			throw new ApiError(401, 'Session expired');
		}
	}

	if (!response.ok) {
		throw new ApiError(response.status, await extractErrorMessage(response));
	}

	return parseBody<T>(response);
}

async function extractErrorMessage(res: Response): Promise<string> {
	const text = await res.text().catch(() => '');
	if (!text) return 'Request failed';
	try {
		const parsed = JSON.parse(text) as { message?: unknown };
		if (typeof parsed.message === 'string' && parsed.message) return parsed.message;
	} catch {
		// Corpo non JSON: mostriamo il testo grezzo così com'è.
	}
	return text;
}

async function parseBody<T>(res: Response): Promise<T> {
	if (res.status === 204) return undefined as T;
	const contentLength = res.headers.get('content-length');
	if (contentLength === '0') return undefined as T;
	return res.json() as Promise<T>;
}

// ---------------------------------------------------------------------------
// HTTP helpers
// ---------------------------------------------------------------------------

export const api = {
	get: <T>(path: string) => request<T>(path),
	post: <T>(path: string, body?: unknown) =>
		request<T>(path, {
			method: 'POST',
			body: body !== undefined ? JSON.stringify(body) : undefined,
		}),
	put: <T>(path: string, body?: unknown) =>
		request<T>(path, {
			method: 'PUT',
			body: body !== undefined ? JSON.stringify(body) : undefined,
		}),
	del: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};

// ---------------------------------------------------------------------------
// Domain API helpers
// ---------------------------------------------------------------------------

export const authApi = {
	login: (email: string, password: string) =>
		api.post<Tokens>('/auth/login', { email, password }),

	logout: () => api.post<void>('/auth/logout'),
};

export const userApi = {
	getById: (id: string) => api.get<UserDTO>(`/user/${id}`),

	getAll: () => api.get<UserDTO[]>('/user'),

	signup: (data: UserRegisterPayload) => api.post<void>('/user/signup', data),

	invite: (id: string) => api.post<{ inviteToken: string }>(`/user/${id}/invite`),

	acceptInvite: (token: string, password: string) =>
		api.post<void>('/user/invite/accept', { token, password }),

	changePassword: (payload: ChangePasswordPayload) =>
		api.post<void>('/user/password', payload),
};

export const shiftApi = {
	getByDateRange: (startDate: string, endDate: string) =>
		api.get<ShiftDTO[]>(`/shift?startDate=${startDate}&endDate=${endDate}`),

	create: (payload: ShiftCreatePayload) => api.post<ShiftDTO>('/shift', payload),

	update: (id: string, payload: ShiftUpdatePayload) => api.put<ShiftDTO>(`/shift/${id}`, payload),

	del: (id: string) => api.del<void>(`/shift/${id}`),
};
