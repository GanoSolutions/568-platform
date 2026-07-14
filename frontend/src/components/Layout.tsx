import { Outlet, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '@/context/AuthContext';
import { useRequests } from '@/context/RequestsContext';

export default function Layout() {
	const { user, logout } = useAuth();
	const { pendingCount } = useRequests();
	const navigate = useNavigate();

	const handleLogout = () => {
		logout();
		navigate('/login');
	};

	return (
		<div className="min-h-screen w-full bg-slate-50 flex flex-col">
			{/* Header */}
			<header className="bg-slate-900 text-white px-4 py-3 flex items-center justify-between sticky top-0 z-10 shadow-md">
				<div className="flex items-center gap-2">
					<img
						src="/logo.png"
						alt="568"
						className="w-8 h-8 rounded-full object-cover"
					/>
					<span className="font-bold text-base">Turni 568</span>
				</div>
				<div className="flex items-center gap-3">
					<button
						onClick={() => navigate('/profile')}
						aria-label="Il mio profilo"
						className="w-7 h-7 rounded-full flex items-center justify-center text-white text-xs font-bold hover:ring-2 hover:ring-white/30 transition"
						style={{ backgroundColor: user?.color }}
					>
						{user?.name?.charAt(0)}
					</button>
					<button onClick={handleLogout} className="text-slate-400 hover:text-white transition">
						<svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
						</svg>
					</button>
				</div>
			</header>

			{/* Contenuto pagina */}
			<main className="flex-1 overflow-y-auto pb-20">
				<Outlet />
			</main>

			{/* Bottom Navigation */}
			<nav className="fixed inset-x-0 bottom-0 w-full bg-white border-t border-slate-200 flex z-10">
				<NavLink
					to="/calendar"
					className={({ isActive }) =>
						`flex-1 flex flex-col items-center gap-1 py-3 text-xs font-medium transition ${isActive ? 'text-indigo-600' : 'text-slate-400 hover:text-slate-600'}`
					}
				>
					<svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
					</svg>
					Calendario
				</NavLink>

				<NavLink
					to="/requests"
					className={({ isActive }) =>
						`flex-1 flex flex-col items-center gap-1 py-3 text-xs font-medium transition relative ${isActive ? 'text-indigo-600' : 'text-slate-400 hover:text-slate-600'}`
					}
				>
					{pendingCount > 0 && (
						<span className="absolute top-2 right-[28%] min-w-4 h-4 px-1 rounded-full bg-red-500 text-white text-[10px] leading-4 text-center font-bold">
							{pendingCount > 9 ? '9+' : pendingCount}
						</span>
					)}
					<svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 10h.01M12 10h.01M16 10h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4-.8L3 20l1.2-4.2A7.64 7.64 0 013 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
					</svg>
					Richieste
				</NavLink>

				{user?.role === 'admin' && (
					<NavLink
						to="/employees"
						className={({ isActive }) =>
							`flex-1 flex flex-col items-center gap-1 py-3 text-xs font-medium transition ${isActive ? 'text-indigo-600' : 'text-slate-400 hover:text-slate-600'}`
						}
					>
						<svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
						</svg>
						Dipendenti
					</NavLink>
				)}
			</nav>
		</div>
	);
}
