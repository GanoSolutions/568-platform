import { useState } from 'react';
import { useEmployees } from '@/hooks/useEmployees';
import EmployeeSheet from '@/components/employees/EmployeeSheet';
import DeleteConfirmDialog from '@/components/employees/DeleteConfirmDialog';
import { Spinner } from '@/components/ui/spinner';
import type { EmployeeDetail } from '@/types';

export default function Employees() {
	const { employees, loading, error, addEmployee, updateEmployee, deleteEmployee, sendInvite, copyInviteLink } = useEmployees();
	const [sheet, setSheet] = useState<'create' | EmployeeDetail | null>(null);
	const [toDelete, setToDelete] = useState<EmployeeDetail | null>(null);
	const [inviteSent, setInviteSent] = useState<string | null>(null);
	const [inviting, setInviting] = useState<string | null>(null);
	const [copying, setCopying] = useState<string | null>(null);
	const [copied, setCopied] = useState<string | null>(null);
	const [deleting, setDeleting] = useState(false);
	const [actionError, setActionError] = useState('');

	const handleSave = async (data: { name: string; fiscalCode: string; email: string; phone: string; contractEnd: string }) => {
		setActionError('');
		try {
			if (sheet === 'create') {
				await addEmployee(data);
			} else if (sheet) {
				await updateEmployee(sheet.id, data);
			}
			setSheet(null);
		} catch (saveError) {
			setActionError((saveError as Error).message || 'Salvataggio non riuscito');
			throw saveError;
		}
	};

	const handleDelete = async () => {
		setActionError('');
		setDeleting(true);
		try {
			await deleteEmployee(toDelete!.id);
			setToDelete(null);
		} catch (deleteError) {
			setActionError((deleteError as Error).message || 'Eliminazione non riuscita');
		} finally {
			setDeleting(false);
		}
	};

	const handleInvite = async (emp: EmployeeDetail) => {
		setActionError('');
		setInviting(emp.id);
		try {
			await sendInvite(emp.id);
			setInviteSent(emp.id);
			setTimeout(() => setInviteSent(null), 3000);
		} catch (inviteError) {
			setActionError((inviteError as Error).message || 'Invito non riuscito');
		} finally {
			setInviting(null);
		}
	};

	const handleCopyLink = async (emp: EmployeeDetail) => {
		setActionError('');
		setCopying(emp.id);
		try {
			const link = await copyInviteLink(emp.id);
			await navigator.clipboard.writeText(link);
			setCopied(emp.id);
			setTimeout(() => setCopied(null), 3000);
		} catch (copyError) {
			setActionError((copyError as Error).message || 'Copia link non riuscita');
		} finally {
			setCopying(null);
		}
	};

	return (
		<div className="flex flex-col">
			{/* Header con bottone aggiungi */}
			<div className="bg-white border-b border-slate-200 px-4 py-3 flex items-center justify-between">
				<div>
					<p className="font-bold text-slate-800">Dipendenti</p>
					<p className="text-xs text-slate-500 mt-0.5">{employees.length} in anagrafica</p>
				</div>
				<button
					onClick={() => setSheet('create')}
					className="flex items-center gap-1.5 bg-indigo-500 hover:bg-indigo-400 text-white text-sm font-semibold px-4 py-2 rounded-xl transition shadow-sm shadow-indigo-500/20"
				>
					<svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M12 4v16m8-8H4" />
					</svg>
					Crea profilo
				</button>
			</div>

			{(error || actionError) && (
				<div className="mx-4 mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
					{actionError || error}
				</div>
			)}

			{/* Lista */}
			<div className="divide-y divide-slate-100">
				{loading && (
					<div className="flex flex-col items-center text-center py-16 px-8 bg-white">
						<div className="w-8 h-8 rounded-full border-2 border-slate-200 border-t-indigo-500 animate-spin mb-4" />
						<p className="text-slate-500 text-sm">Caricamento dipendenti...</p>
					</div>
				)}

				{!loading && employees.length === 0 && (
					<div className="flex flex-col items-center text-center py-16 px-8">
						<div className="w-14 h-14 bg-slate-100 rounded-full flex items-center justify-center mb-4">
						<svg className="w-7 h-7 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
						</svg>
					</div>
					<p className="text-slate-500 text-sm">Nessun dipendente in anagrafica</p>
					<p className="text-slate-500 text-xs mt-1">Usa &quot;Crea profilo&quot; per aggiungerne uno</p>
					</div>
				)}

				{!loading && employees.map((emp) => (
					<div key={emp.id} className="bg-white px-4 py-4">
						{/* Riga nome + avatar */}
						<div className="flex items-center gap-3 mb-3">
							<div
								className="w-10 h-10 rounded-full flex items-center justify-center text-white text-sm font-bold shrink-0"
								style={{ backgroundColor: emp.color }}
							>
								{emp.name.charAt(0)}
							</div>
							<div className="flex-1 min-w-0">
								<p className="font-semibold text-slate-800 truncate">{emp.name}</p>
								<p className="text-xs text-slate-500 truncate">{emp.email}</p>
							</div>
							{/* Badge invito */}
						{emp.firstLoginCompleted ? (
							<span className="text-[10px] font-semibold text-indigo-600 bg-indigo-50 border border-indigo-200 px-2 py-0.5 rounded-full shrink-0">
								Attivo
							</span>
						) : emp.invited ? (
								<span className="text-[10px] font-semibold text-green-600 bg-green-50 border border-green-200 px-2 py-0.5 rounded-full shrink-0">
									Invitato
								</span>
							) : (
								<span className="text-[10px] font-semibold text-amber-600 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded-full shrink-0">
									Non invitato
								</span>
							)}
						</div>

						{/* Dettagli */}
						<div className="bg-slate-50 rounded-xl px-3 py-2.5 space-y-1.5 mb-3">
							<Detail icon="phone" label={emp.phone} />
							{emp.contractEnd && (
								<Detail icon="calendar" label={`Fine contratto: ${formatDate(emp.contractEnd)}`} warn />
							)}
						</div>

						{/* Azioni */}
						<div className="flex gap-2">
							{!emp.invited && (
								<button
									onClick={() => handleInvite(emp)}
									disabled={inviting === emp.id}
									className="flex-1 flex items-center justify-center gap-1.5 text-xs font-semibold text-indigo-600 bg-indigo-50 hover:bg-indigo-100 border border-indigo-200 py-2 rounded-xl transition disabled:opacity-70"
								>
									{inviteSent === emp.id ? (
										<><svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" /></svg> Inviato!</>
									) : inviting === emp.id ? (
										<><Spinner className="w-3.5 h-3.5" /> Invio...</>
									) : (
										<><svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" /></svg> Invia invito</>
									)}
								</button>
							)}
							<button
								onClick={() => handleCopyLink(emp)}
								disabled={copying === emp.id}
								className="flex-1 flex items-center justify-center gap-1.5 text-xs font-semibold text-emerald-600 bg-emerald-50 hover:bg-emerald-100 border border-emerald-200 py-2 rounded-xl transition disabled:opacity-70"
							>
								{copied === emp.id ? (
									<><svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" /></svg> Copiato!</>
								) : copying === emp.id ? (
									<><Spinner className="w-3.5 h-3.5" /> Genero...</>
								) : (
									<><svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" /></svg> Copia link</>
								)}
							</button>
							<button
								onClick={() => setSheet(emp)}
								className="flex-1 flex items-center justify-center gap-1.5 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 py-2 rounded-xl transition"
							>
								<svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
									<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
								</svg>
								Modifica
							</button>
							<button
								onClick={() => setToDelete(emp)}
							className="w-10 flex items-center justify-center text-red-600 bg-red-50 hover:bg-red-100 rounded-xl transition"
							>
								<svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
									<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
								</svg>
							</button>
						</div>
					</div>
				))}
			</div>

			{/* Sheet crea/modifica */}
			{sheet !== null && (
				<EmployeeSheet
					employee={sheet === 'create' ? null : sheet}
					onSave={handleSave}
					onClose={() => setSheet(null)}
					saveError={actionError}
				/>
			)}

			{/* Dialog conferma eliminazione */}
			{toDelete && (
				<DeleteConfirmDialog
					employee={toDelete}
					deleting={deleting}
					onConfirm={handleDelete}
					onClose={() => setToDelete(null)}
				/>
			)}
		</div>
	);
}

function Detail({ icon, label, warn = false }: { icon: string; label: string; warn?: boolean }) {
	return (
		<div className="flex items-center gap-2">
			<svg className={`w-3.5 h-3.5 shrink-0 ${warn ? 'text-amber-500' : 'text-slate-400'}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
				{icon === 'phone' ? (
					<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
				) : (
					<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
				)}
			</svg>
			<span className={`text-xs ${warn ? 'text-amber-600 font-medium' : 'text-slate-500'}`}>{label}</span>
		</div>
	);
}

function formatDate(dateStr: string): string {
	if (!dateStr) return '';
	const [y, m, d] = dateStr.split('-');
	return `${d}/${m}/${y}`;
}
