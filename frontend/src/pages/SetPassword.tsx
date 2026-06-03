import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '@/context/AuthContext';
import { userApi } from '@/lib/apiClient';

export default function SetPassword() {
	const navigate = useNavigate();
	const [searchParams] = useSearchParams();
	const { user, logout } = useAuth();
	const [password, setPassword] = useState('');
	const [confirm, setConfirm] = useState('');
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState('');

	const inviteToken = searchParams.get('token');

	const validate = (): string | null => {
		if (password.length < 8) return 'La password deve essere di almeno 8 caratteri';
		if (password !== confirm) return 'Le password non coincidono';
		return null;
	};

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		const validationError = validate();
		if (validationError) { setError(validationError); return; }

		if (!inviteToken) { setError('Link di invito non valido o scaduto. Chiedi un nuovo invito.'); return; }

		setSubmitting(true);
		setError('');

		try {
			await userApi.acceptInvite(inviteToken, password);
			navigate('/login', { replace: true });
		} catch (err) {
			setError((err as Error).message ?? 'Errore durante il salvataggio della password');
		} finally {
			setSubmitting(false);
		}
	};

	return (
		<div className="min-h-screen bg-slate-900 flex items-center justify-center px-6">
			<div className="w-full max-w-sm">
				{/* Logo / Header */}
				<div className="text-center mb-8">
					<div className="w-14 h-14 bg-indigo-500 rounded-2xl flex items-center justify-center mx-auto mb-4">
						<svg className="w-7 h-7 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
						</svg>
					</div>
					<h1 className="text-xl font-bold text-white">Imposta la tua password</h1>
					<p className="text-slate-400 text-sm mt-1">
						{user?.name ? `Benvenuto/a, ${user.name}!` : 'Benvenuto/a!'} Scegli una password per accedere all&apos;app.
					</p>
					{!inviteToken && (
						<p className="text-red-400 text-xs mt-2 bg-red-950/40 border border-red-900 rounded-lg px-3 py-2">
							Link di invito mancante. Assicurati di aver aperto il link dall&apos;email di invito.
						</p>
					)}
				</div>

				{/* Form */}
				<form onSubmit={handleSubmit} className="space-y-4">
					<div>
						<label className="text-slate-400 text-xs font-semibold uppercase tracking-wider block mb-1.5">
							Nuova password
						</label>
						<input
							type="password"
							value={password}
							onChange={e => setPassword(e.target.value)}
							placeholder="Minimo 8 caratteri"
							autoComplete="new-password"
							required
						className="w-full bg-slate-800 border border-slate-700 rounded-xl px-4 py-3 text-sm text-white outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 transition placeholder:text-slate-400"
						/>
					</div>

					<div>
						<label className="text-slate-400 text-xs font-semibold uppercase tracking-wider block mb-1.5">
							Conferma password
						</label>
						<input
							type="password"
							value={confirm}
							onChange={e => setConfirm(e.target.value)}
							placeholder="Ripeti la password"
							autoComplete="new-password"
							required
						className="w-full bg-slate-800 border border-slate-700 rounded-xl px-4 py-3 text-sm text-white outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 transition placeholder:text-slate-400"
						/>
					</div>

					{error && (
						<div className="bg-red-500/10 border border-red-500/30 rounded-xl px-4 py-3 text-sm text-red-400">
							{error}
						</div>
					)}

					<button
						type="submit"
						disabled={submitting || !password || !confirm}
						className="w-full bg-indigo-500 hover:bg-indigo-400 disabled:opacity-60 text-white font-semibold rounded-xl py-3 text-sm transition mt-2"
					>
						{submitting ? 'Salvataggio...' : 'Accedi all\'app →'}
					</button>
				</form>

				{/* Escape hatch: visibile solo se un utente è già loggato (caso anomalo) */}
				{user && (
					<div className="mt-6 text-center">
						<p className="text-slate-600 text-xs mb-2">
							Accesso come <span className="text-slate-400">{user.email}</span>
						</p>
						<button
							type="button"
							onClick={async () => { await logout(); navigate('/login', { replace: true }); }}
							className="text-slate-500 hover:text-slate-300 text-xs underline underline-offset-2 transition"
						>
							Non sei tu? Esci
						</button>
					</div>
				)}
			</div>
		</div>
	);
}
