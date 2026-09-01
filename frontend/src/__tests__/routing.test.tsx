import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route, Navigate } from 'react-router-dom';
import React from 'react';
import { ProtectedRoute } from '@/App';

// Mock useAuth — ogni test può sovrascrivere il valore con mockReturnValue
vi.mock('@/context/AuthContext', () => ({
	useAuth: vi.fn(),
	AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

// Mock pagine pesanti — non ci interessa renderle nel test di routing
vi.mock('@/pages/Calendar', () => ({ default: () => <div>calendar</div> }));
vi.mock('@/pages/Employees', () => ({ default: () => <div>employees</div> }));
vi.mock('@/pages/Requests', () => ({ default: () => <div>requests</div> }));
vi.mock('@/pages/Login', () => ({ default: () => <div>login page</div> }));
vi.mock('@/pages/SetPassword', () => ({ default: () => <div>set password page</div> }));
vi.mock('@/components/Layout', () => ({ default: () => <div>layout</div> }));
vi.mock('@/context/RequestsContext', () => ({
	RequestsProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
	useRequests: vi.fn(() => ({})),
}));

import { useAuth } from '@/context/AuthContext';

// Helper per creare un utente di test
function makeUser(overrides = {}) {
	return {
		id: 'profile-1',
		authUserId: 'auth-1',
		name: 'Test User',
		email: 'test@example.com',
		role: 'employee' as const,
		isAdmin: false,
		color: '#6366f1',
		firstLoginCompleted: true,
		...overrides,
	};
}

// ─── B1: ProtectedRoute ───────────────────────────────────────────────────────

describe('ProtectedRoute', () => {
	beforeEach(() => {
		vi.mocked(useAuth).mockReturnValue({
			user: null,
			loading: false,
			login: vi.fn(),
			loginWithPassword: vi.fn(),
			logout: vi.fn(),
			refreshProfile: vi.fn(),
			completeFirstLogin: vi.fn(),
			isMockMode: false,
		});
	});

	it('utente non loggato → redirect a /login', () => {
		vi.mocked(useAuth).mockReturnValue({
			...vi.mocked(useAuth)(),
			user: null,
			loading: false,
		});

		render(
			<MemoryRouter initialEntries={['/calendar']}>
				<Routes>
					<Route path="/calendar" element={<ProtectedRoute><div>calendar</div></ProtectedRoute>} />
					<Route path="/login" element={<div>login page</div>} />
				</Routes>
			</MemoryRouter>
		);

		expect(screen.getByText('login page')).toBeInTheDocument();
		expect(screen.queryByText('calendar')).not.toBeInTheDocument();
	});

	it('utente con firstLoginCompleted=false → redirect a /set-password', () => {
		vi.mocked(useAuth).mockReturnValue({
			...vi.mocked(useAuth)(),
			user: makeUser({ firstLoginCompleted: false }),
			loading: false,
		});

		render(
			<MemoryRouter initialEntries={['/calendar']}>
				<Routes>
					<Route path="/calendar" element={<ProtectedRoute><div>calendar</div></ProtectedRoute>} />
					<Route path="/set-password" element={<div>set password page</div>} />
				</Routes>
			</MemoryRouter>
		);

		expect(screen.getByText('set password page')).toBeInTheDocument();
		expect(screen.queryByText('calendar')).not.toBeInTheDocument();
	});

	it('utente autenticato con firstLoginCompleted=true → renderizza i figli', () => {
		vi.mocked(useAuth).mockReturnValue({
			...vi.mocked(useAuth)(),
			user: makeUser({ firstLoginCompleted: true }),
			loading: false,
		});

		render(
			<MemoryRouter initialEntries={['/calendar']}>
				<Routes>
					<Route path="/calendar" element={<ProtectedRoute><div>calendar</div></ProtectedRoute>} />
				</Routes>
			</MemoryRouter>
		);

		expect(screen.getByText('calendar')).toBeInTheDocument();
	});

	it('admin autenticato → renderizza i figli (ProtectedRoute non blocca per ruolo)', () => {
		vi.mocked(useAuth).mockReturnValue({
			...vi.mocked(useAuth)(),
			user: makeUser({ role: 'admin', firstLoginCompleted: true }),
			loading: false,
		});

		render(
			<MemoryRouter initialEntries={['/employees']}>
				<Routes>
					<Route path="/employees" element={<ProtectedRoute><div>employees</div></ProtectedRoute>} />
				</Routes>
			</MemoryRouter>
		);

		expect(screen.getByText('employees')).toBeInTheDocument();
	});
});

// ─── B2: /login route redirect logic ─────────────────────────────────────────

// Riproduce esattamente la logica del route /login in App.tsx:
// !user → Login | user.firstLoginCompleted → /calendar | else → /set-password
function LoginRouteLogic({ user }: { user: ReturnType<typeof makeUser> | null }) {
	if (!user) return <div>login page</div>;
	if (user.firstLoginCompleted) return <Navigate to="/calendar" replace />;
	return <Navigate to="/set-password" replace />;
}

describe('logica redirect route /login', () => {
	it('utente non loggato → mostra il form di login', () => {
		render(
			<MemoryRouter initialEntries={['/login']}>
				<Routes>
					<Route path="/login" element={<LoginRouteLogic user={null} />} />
				</Routes>
			</MemoryRouter>
		);

		expect(screen.getByText('login page')).toBeInTheDocument();
	});

	it('utente loggato con firstLoginCompleted=true → redirect a /calendar', () => {
		const user = makeUser({ firstLoginCompleted: true });
		render(
			<MemoryRouter initialEntries={['/login']}>
				<Routes>
					<Route path="/login" element={<LoginRouteLogic user={user} />} />
					<Route path="/calendar" element={<div>calendar</div>} />
				</Routes>
			</MemoryRouter>
		);

		expect(screen.getByText('calendar')).toBeInTheDocument();
		expect(screen.queryByText('login page')).not.toBeInTheDocument();
	});

	it('utente loggato con firstLoginCompleted=false → redirect a /set-password (non loop su /calendar)', () => {
		const user = makeUser({ firstLoginCompleted: false });
		render(
			<MemoryRouter initialEntries={['/login']}>
				<Routes>
					<Route path="/login" element={<LoginRouteLogic user={user} />} />
					<Route path="/calendar" element={<div>calendar</div>} />
					<Route path="/set-password" element={<div>set password page</div>} />
				</Routes>
			</MemoryRouter>
		);

		// Fix: non deve rimbalzare su /calendar, deve andare su /set-password
		expect(screen.getByText('set password page')).toBeInTheDocument();
		expect(screen.queryByText('calendar')).not.toBeInTheDocument();
		expect(screen.queryByText('login page')).not.toBeInTheDocument();
	});
});
