import { useState } from 'react';
import { useAuth } from '@/context/AuthContext';
import ChangePasswordModal from '@/components/profile/ChangePasswordModal';
import { getAvatarTextColor } from '@/lib/colorUtils';

export default function Profile() {
	const { user } = useAuth();
	const [showChangePassword, setShowChangePassword] = useState(false);
	const [passwordUpdated, setPasswordUpdated] = useState(false);

	if (!user) return null;

	return (
		<div className="flex flex-col">
			<div className="bg-white border-b border-slate-200 px-4 py-3">
				<p className="font-bold text-slate-800">Il mio profilo</p>
			</div>

			{passwordUpdated && (
				<div className="mx-4 mt-4 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
					Password aggiornata con successo.
				</div>
			)}

			<div className="p-4">
				<div className="bg-white rounded-2xl border border-slate-200 p-5 flex flex-col items-center text-center">
					<div
						className="w-16 h-16 rounded-full flex items-center justify-center text-lg font-bold mb-3"
						style={{ backgroundColor: user.color, color: getAvatarTextColor(user.color) }}
					>
						{user.name.charAt(0)}
					</div>
					<p className="font-semibold text-slate-800">{user.name}</p>
					<p className="text-sm text-slate-500">{user.email}</p>
					<span className="mt-2 text-[10px] font-semibold text-indigo-600 bg-indigo-50 border border-indigo-200 px-2 py-0.5 rounded-full">
						{user.role === 'admin' ? 'Amministratore' : 'Dipendente'}
					</span>
				</div>

				<button
					onClick={() => { setPasswordUpdated(false); setShowChangePassword(true); }}
					className="mt-4 w-full flex items-center justify-center gap-1.5 bg-indigo-500 hover:bg-indigo-400 text-white text-sm font-semibold px-4 py-3 rounded-xl transition shadow-sm shadow-indigo-500/20"
				>
					Cambia password
				</button>
			</div>

			{showChangePassword && (
				<ChangePasswordModal
					onClose={() => setShowChangePassword(false)}
					onSuccess={() => { setShowChangePassword(false); setPasswordUpdated(true); }}
				/>
			)}
		</div>
	);
}
