import { useState } from 'react';
import { Check, Eye, EyeOff, X } from 'lucide-react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { ApiError, userApi } from '@/lib/apiClient';

interface ChangePasswordModalProps {
	onClose: () => void
	onSuccess: () => void
}

// Rispecchia AppSettings:PasswordRequirements sul backend (vedi appsettings.Development.json).
const PASSWORD_REQUIREMENTS: { label: string, test: (password: string) => boolean }[] = [
	{ label: 'Almeno 8 caratteri', test: p => p.length >= 8 },
	{ label: 'Massimo 128 caratteri', test: p => p.length <= 128 },
	{ label: 'Una lettera maiuscola', test: p => /[A-Z]/.test(p) },
	{ label: 'Una lettera minuscola', test: p => /[a-z]/.test(p) },
	{ label: 'Un numero', test: p => /[0-9]/.test(p) },
	{ label: 'Un carattere speciale', test: p => /[^A-Za-z0-9]/.test(p) },
];

function PasswordChecklist({ password }: { password: string }) {
	return (
		<ul className="space-y-1 mt-2">
			{PASSWORD_REQUIREMENTS.map(({ label, test }) => {
				const met = test(password);
				return (
					<li key={label} className={`flex items-center gap-1.5 text-xs ${met ? 'text-emerald-600' : 'text-slate-400'}`}>
						{met ? <Check className="w-3.5 h-3.5 shrink-0" /> : <X className="w-3.5 h-3.5 shrink-0" />}
						{label}
					</li>
				);
			})}
		</ul>
	);
}

function PasswordField({
	label, value, onChange, autoComplete,
}: {
	label: string
	value: string
	onChange: (value: string) => void
	autoComplete: string
}) {
	const [visible, setVisible] = useState(false);

	return (
		<div>
			<label className="text-slate-500 text-xs font-semibold uppercase tracking-wider block mb-1.5">
				{label}
			</label>
			<div className="relative">
				<input
					type={visible ? 'text' : 'password'}
					value={value}
					onChange={e => onChange(e.target.value)}
					autoComplete={autoComplete}
					required
					className="w-full bg-slate-50 border border-slate-200 rounded-xl pl-4 pr-10 py-2.5 text-sm text-slate-800 outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 transition"
				/>
				<button
					type="button"
					onClick={() => setVisible(v => !v)}
					className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
					tabIndex={-1}
					aria-label={visible ? 'Nascondi password' : 'Mostra password'}
				>
					{visible ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
				</button>
			</div>
		</div>
	);
}

export default function ChangePasswordModal({ onClose, onSuccess }: ChangePasswordModalProps) {
	const [currentPassword, setCurrentPassword] = useState('');
	const [newPassword, setNewPassword] = useState('');
	const [confirmPassword, setConfirmPassword] = useState('');
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState('');

	const newPasswordValid = PASSWORD_REQUIREMENTS.every(({ test }) => test(newPassword));
	const passwordsMatch = newPassword === confirmPassword;
	const canSubmit = currentPassword.length > 0 && newPasswordValid && passwordsMatch;

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		if (!canSubmit) return;

		setSubmitting(true);
		setError('');
		try {
			await userApi.changePassword({ currentPassword, newPassword });
			onSuccess();
		} catch (err) {
			setError(err instanceof ApiError ? err.message : 'Impossibile completare la richiesta. Riprova.');
		} finally {
			setSubmitting(false);
		}
	};

	return (
		<Dialog open onOpenChange={onClose}>
			<DialogContent className="max-w-sm w-[92vw] rounded-2xl p-0 overflow-hidden gap-0">
				<form onSubmit={handleSubmit}>
					<DialogHeader className="px-5 pt-5 pb-4">
						<DialogTitle className="text-slate-800">Cambia password</DialogTitle>
					</DialogHeader>

					<div className="px-5 pb-2 space-y-4">
						<PasswordField
							label="Password attuale"
							value={currentPassword}
							onChange={setCurrentPassword}
							autoComplete="current-password"
						/>
						<div>
							<PasswordField
								label="Nuova password"
								value={newPassword}
								onChange={setNewPassword}
								autoComplete="new-password"
							/>
							{newPassword.length > 0 && <PasswordChecklist password={newPassword} />}
						</div>
						<div>
							<PasswordField
								label="Conferma nuova password"
								value={confirmPassword}
								onChange={setConfirmPassword}
								autoComplete="new-password"
							/>
							{confirmPassword.length > 0 && !passwordsMatch && (
								<p className="text-xs text-red-600 mt-1.5">Le password non coincidono</p>
							)}
						</div>

						{error && (
							<div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-xs text-red-700">
								{error}
							</div>
						)}
					</div>

					<DialogFooter className="px-5 pb-5 flex gap-2 border-t-0 bg-transparent mx-0 mb-0">
						<Button type="button" variant="outline" onClick={onClose} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm">
							Annulla
						</Button>
						<Button type="submit" disabled={!canSubmit || submitting} className="flex-1 rounded-xl py-3 text-sm flex items-center justify-center gap-2">
							{submitting ? (
								<><span className="inline-block w-4 h-4 rounded-full border-2 border-current border-t-transparent animate-spin" /> Salvataggio...</>
							) : 'Salva'}
						</Button>
					</DialogFooter>
				</form>
			</DialogContent>
		</Dialog>
	);
}
