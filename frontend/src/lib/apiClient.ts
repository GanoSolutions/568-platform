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
}

// ---------------------------------------------------------------------------
// Backend DTO types (mirror of C# models — enums are numeric by default)
// ---------------------------------------------------------------------------

/** Numeric values match the C# UserRole enum: Admin=0, Manager=1, Employee=2 */
export type UserRoleCode = 0 | 1 | 2;

/** Numeric values match the C# UserStatus enum: Pending=0, Active=1, Disabled=2 */
export type UserStatusCode = 0 | 1 | 2;

export interface Tokens {
	accessToken: string;
	refreshToken: string;
}

export interface UserDTO {
	id: string;
	email: string;
	fullName: string;
	role: UserRoleCode;
	status: UserStatusCode;
	color: string | null;
	createdAt: string;
}

export interface UserRegisterPayload {
	email: string;
	fullName: string;
	password: string;
	role: UserRoleCode;
}

// ---------------------------------------------------------------------------
// Token storage
// ---------------------------------------------------------------------------

const ACCESS_TOKEN_KEY = 'api_access_token';
const REFRESH_TOKEN_KEY = 'api_refresh_token';

/**
 * Decodes the JWT payload and returns the user ID.
 * Returns null if the token is missing, malformed, or has no `sub`.
 */
export function getUserIdFromToken(token: string): string | null {
	try {
		const payload = jwtDecode<JwtPayload>(token);
		return typeof payload.nameid === 'string' ? payload.nameid : null;
	} catch {
		return null;
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

	const token = getAccessToken();
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
				const body = await retried.text().catch(() => 'Request failed');
				throw new ApiError(retried.status, body);
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
		const body = await response.text().catch(() => 'Request failed');
		throw new ApiError(response.status, body);
	}

	return parseBody<T>(response);
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
};
