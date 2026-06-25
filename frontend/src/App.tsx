import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from '@/context/AuthContext';
import { RequestsProvider } from '@/context/RequestsContext';
import Login from '@/pages/Login';
import Calendar from '@/pages/Calendar';
import Employees from '@/pages/Employees';
import Requests from '@/pages/Requests';
import Layout from '@/components/Layout';
import SetPassword from '@/pages/SetPassword';
import type { ReactNode } from 'react';

export function ProtectedRoute({ children }: { children: ReactNode }) {
	const { user, loading } = useAuth();
	if (loading) return <ScreenLoader />;
	if (!user) return <Navigate to="/login" replace />;
	if (!user.firstLoginCompleted) return <Navigate to="/set-password" replace />;
	return children;
}

function AdminRoute({ children }: { children: ReactNode }) {
	const { user, loading } = useAuth();
	if (loading) return <ScreenLoader />;
	if (!user) return <Navigate to="/login" replace />;
	if (user.role !== 'admin') return <Navigate to="/calendar" replace />;
	return children;
}

function AppRoutes() {
	const { user, loading } = useAuth();
	if (loading) return <ScreenLoader />;

	return (
		<Routes>
			<Route path="/login" element={
				!user
					? <Login />
					: user.firstLoginCompleted
						? <Navigate to="/calendar" replace />
						: <Navigate to="/set-password" replace />
				} />
				<Route
				path="/set-password"
				element={
					user?.firstLoginCompleted
						? <Navigate to="/calendar" replace />
						: <SetPassword />
				}
			/>
			<Route path="/" element={<ProtectedRoute><Layout /></ProtectedRoute>}>
				<Route index element={<Navigate to="/calendar" replace />} />
				<Route path="calendar" element={<Calendar />} />
				<Route path="requests" element={<Requests />} />
				<Route path="employees" element={<AdminRoute><Employees /></AdminRoute>} />
			</Route>
			<Route path="*" element={<Navigate to="/calendar" replace />} />
		</Routes>
	);
}

function ScreenLoader() {
	return (
		<div className="min-h-screen bg-slate-900 flex items-center justify-center px-6">
			<div className="flex flex-col items-center text-center">
				<img
					src="/logo.png"
					alt="Birreria 568 Garbatella"
					className="w-20 h-20 rounded-full object-contain animate-pulse"
				/>
				<div className="w-8 h-8 rounded-full border-2 border-slate-700 border-t-indigo-500 animate-spin mt-6" />
				<p className="text-slate-400 text-sm mt-4">Caricamento...</p>
			</div>
		</div>
	);
}

export default function App() {
	return (
		<BrowserRouter>
			<AuthProvider>
				<RequestsProvider>
					<AppRoutes />
				</RequestsProvider>
			</AuthProvider>
		</BrowserRouter>
	);
}
