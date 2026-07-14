import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import type { AppUser } from '@/types';
import { authApi, userApi, UserStatus, type UserDTO, getAccessToken, setTokens, clearTokens, getUserIdFromToken } from '@/lib/apiClient';

interface AuthContextValue {
	user: AppUser | null
	loading: boolean
	login: (userData: AppUser) => void
	loginWithPassword: (email: string, password: string) => Promise<AppUser>
	logout: () => Promise<void>
	refreshProfile: () => Promise<void>
	/** Marca firstLoginCompleted=true nello stato React in modo sincrono */
	completeFirstLogin: () => void
	isMockMode: boolean
}

const AuthContext = createContext<AuthContextValue | null>(null);

export { getUserIdFromToken };

/**
 * Maps a backend UserDTO to the frontend AppUser shape.
 * - Name/color come from the nested `employee` (null until the invite is accepted);
 *   name falls back to the email when there is no employee record yet.
 * - Role: Admin=0, Manager=1 → 'admin'; Employee=2 → 'employee'
 * - firstLoginCompleted: Active(2) or Disabled(3) mean the first login is done
 * - telegramLinked: not yet exposed in UserDTO, defaults to false
 */
export function mapUserDTO(dto: UserDTO): AppUser {
	const emp = dto.employee;
	return {
		id: dto.id,
		name: emp ? `${emp.name} ${emp.surname}`.trim() : dto.email,
		email: dto.email,
		role: dto.role <= 1 ? 'admin' : 'employee',
		color: emp?.color ?? '#6366f1',
		firstLoginCompleted: dto.status >= UserStatus.Active,
		telegramLinked: false,
	};
}

export function AuthProvider({ children }: { children: ReactNode }) {
	const [user, setUser] = useState<AppUser | null>(null);
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		let mounted = true;

		const bootstrap = async () => {
			const token = getAccessToken();
			if (!token) {
				if (mounted) setLoading(false);
				return;
			}

			const userId = getUserIdFromToken(token);
			if (!userId) {
				clearTokens();
				if (mounted) setLoading(false);
				return;
			}

			try {
				const dto = await userApi.getById(userId);
				if (mounted) {
					setUser(mapUserDTO(dto));
					window.dispatchEvent(new Event('app:login'));
				}
			} catch {
				clearTokens();
			} finally {
				if (mounted) setLoading(false);
			}
		};

		bootstrap();

		// apiClient dispatches 'app:logout' when the refresh token also expires
		const handleExternalLogout = () => {
			if (mounted) setUser(null);
		};
		window.addEventListener('app:logout', handleExternalLogout);

		return () => {
			mounted = false;
			window.removeEventListener('app:logout', handleExternalLogout);
		};
	}, []);

	const login = (userData: AppUser) => setUser(userData);

	const loginWithPassword = async (email: string, password: string): Promise<AppUser> => {
		const tokens = await authApi.login(email, password);
		setTokens(tokens);

		const userId = getUserIdFromToken(tokens.accessToken);
		if (!userId) throw new Error('Token non valido ricevuto dal server');

		const dto = await userApi.getById(userId);
		const appUser = mapUserDTO(dto);
		setUser(appUser);
		window.dispatchEvent(new Event('app:login'));
		return appUser;
	};

	const logout = async () => {
		window.dispatchEvent(new Event('app:logout'));
		try {
			await authApi.logout();
		} catch { /* tokens vengono puliti comunque */ }
		clearTokens();
		setUser(null);
	};

	const refreshProfile = async () => {
		if (!user) return;
		const dto = await userApi.getById(user.id);
		setUser(mapUserDTO(dto));
	};

	// Aggiornamento ottimistico sincrono — evita dipendenza da round-trip dopo
	// l'accettazione dell'invito, dove lo status cambia da Pending ad Active
	const completeFirstLogin = () => {
		setUser(prev => prev ? { ...prev, firstLoginCompleted: true } : prev);
	};

	return (
		<AuthContext.Provider value={{ user, loading, login, loginWithPassword, logout, refreshProfile, completeFirstLogin, isMockMode: false }}>
			{children}
		</AuthContext.Provider>
	);
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
	const ctx = useContext(AuthContext);
	if (!ctx) throw new Error('useAuth must be used within AuthProvider');
	return ctx;
}
