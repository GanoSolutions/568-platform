import { useState } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { useAuth } from '@/context/AuthContext';
import { Button } from '@/components/ui/button';
import type { AppUser } from '@/types';

export default function Login() {
	const { login, loginWithPassword, isMockMode } = useAuth();
	const [email, setEmail] = useState('');
	const [password, setPassword] = useState('');
	const [showPassword, setShowPassword] = useState(false);
	const [error, setError] = useState('');
	const [submitting, setSubmitting] = useState(false);

	const handleSubmit = async (e: React.SubmitEvent) => {
		e.preventDefault();
		setError('');
		setSubmitting(true);

		try {
			if (isMockMode) {
				if (email === 'birreria568@gmail.com' && password === 'admin') {
					login({ id: '1', name: 'Amministratore', email, role: 'admin', color: '#6366f1', firstLoginCompleted: true } as AppUser);
				} else if (email === 'test@test.com' && password === 'test') {
					login({ id: '2', name: 'Mario Rossi', email, role: 'employee', color: '#f43f5e', firstLoginCompleted: true } as AppUser);
				} else {
					throw new Error('Email o password non corretti');
				}
			} else {
				await loginWithPassword(email, password);
			}
		} catch {
			// Messaggio generico: non riveliamo se l'errore è su email o password.
			setError('Email o password non corretti');
		} finally {
			setSubmitting(false);
		}
	};

	return (
		<div className="min-h-screen bg-slate-900 flex flex-col items-center justify-center px-6">
			{/* Logo / Titolo */}
			<div className="mb-10 text-center">
				<img
					src="/logo.png"
					alt="Birreria 568 Garbatella"
					className="w-24 h-24 mx-auto mb-4 rounded-full object-contain"
				/>
				<h1 className="text-white text-2xl font-bold tracking-tight">Turni 568</h1>
				<p className="text-slate-400 text-sm mt-1">Pianificazione settimanale</p>
			</div>

			{/* Form */}
			<form onSubmit={handleSubmit} className="w-full max-w-sm space-y-4">
				<div>
					<label className="text-slate-300 text-sm font-medium block mb-1.5">Email</label>
					<input
						type="email"
						value={email}
						onChange={(e) => setEmail(e.target.value)}
						className="w-full bg-slate-800 text-white border border-slate-700 rounded-xl px-4 py-3 text-sm outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 transition"
						placeholder="la-tua@email.com"
						required
						autoComplete="email"
					/>
				</div>
				<div>
					<label className="text-slate-300 text-sm font-medium block mb-1.5">Password</label>
					<div className="relative">
						<input
							type={showPassword ? 'text' : 'password'}
							value={password}
							onChange={(e) => setPassword(e.target.value)}
							className="w-full bg-slate-800 text-white border border-slate-700 rounded-xl px-4 py-3 pr-12 text-sm outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 transition"
							placeholder="••••••••"
							required
							autoComplete="current-password"
						/>
						<button
							type="button"
							onClick={() => setShowPassword((v) => !v)}
							aria-label={showPassword ? 'Nascondi password' : 'Mostra password'}
							className="absolute inset-y-0 right-0 flex items-center px-3 text-slate-400 hover:text-slate-200 transition"
						>
							{showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
						</button>
					</div>
				</div>

				{error && (
					<p className="text-red-400 text-sm text-center bg-red-950/50 border border-red-900 rounded-lg py-2">{error}</p>
				)}

				<Button type="submit" disabled={submitting} className="w-full mt-2 bg-indigo-500 hover:bg-indigo-400 text-white font-semibold py-3 rounded-xl text-sm transition shadow-lg shadow-indigo-500/20 disabled:opacity-70">
					{submitting ? 'Accesso in corso...' : 'Accedi'}
				</Button>
			</form>

			<div className="text-center mt-8 space-y-1">
				<p className="text-slate-400 text-xs">Accesso riservato ai membri del team</p>
				{isMockMode && (
					<p className="text-amber-400/80 text-[11px]">Modalita demo attiva: Supabase non ancora operativo lato dati</p>
				)}
			</div>
		</div>
	);
}
