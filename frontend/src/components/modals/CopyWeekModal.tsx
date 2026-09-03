import { useMemo, useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';

function formatInputDate(date: Date): string {
	const year = date.getFullYear();
	const month = String(date.getMonth() + 1).padStart(2, '0');
	const day = String(date.getDate()).padStart(2, '0');
	return `${year}-${month}-${day}`;
}

function addDays(date: Date, amount: number): Date {
	const next = new Date(date);
	next.setDate(next.getDate() + amount);
	return next;
}

interface CopyWeekModalProps {
	currentMonday: Date
	onSubmit: (params: { startDate: string; endDate: string }) => Promise<void>
	onClose: () => void
	submitError: string
}

export default function CopyWeekModal({ currentMonday, onSubmit, onClose, submitError }: CopyWeekModalProps) {
	const defaultStart = useMemo(() => addDays(currentMonday, 7), [currentMonday]);
	const defaultEnd = useMemo(() => addDays(currentMonday, 28), [currentMonday]);

	const [startDate, setStartDate] = useState(formatInputDate(defaultStart));
	const [endDate, setEndDate] = useState(formatInputDate(defaultEnd));
	const [submitting, setSubmitting] = useState(false);
	const [localError, setLocalError] = useState('');

	const handleSubmit = async () => {
		setLocalError('');

		if (!startDate || !endDate) {
			setLocalError('Seleziona sia la data iniziale sia la data finale');
			return;
		}

		if (new Date(`${startDate}T00:00:00`).getDay() !== 1) {
			setLocalError('La data iniziale deve essere un lunedì');
			return;
		}

		if (new Date(`${endDate}T00:00:00`).getDay() !== 0) {
			setLocalError('La data finale deve essere una domenica');
			return;
		}

		if (endDate < startDate) {
			setLocalError('La data finale deve essere successiva o uguale alla data iniziale');
			return;
		}

		setSubmitting(true);
		try {
			await onSubmit({ startDate, endDate });
		} finally {
			setSubmitting(false);
		}
	};

	return (
		<Dialog open onOpenChange={onClose}>
			<DialogContent className="max-w-sm w-[92vw] rounded-2xl p-0 overflow-hidden gap-0">
				<DialogHeader className="px-5 pt-5 pb-4 border-b border-slate-100">
					<DialogTitle className="text-slate-800">Copia settimana</DialogTitle>
				</DialogHeader>

				<div className="px-5 py-4 space-y-4">
					<div className="rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">
						La settimana attualmente visibile verrà copiata su tutte le settimane comprese nel periodo scelto. I turni già presenti verranno sovrascritti.
					</div>

					<div>
						<label className="text-slate-500 text-xs font-semibold uppercase tracking-wider block mb-1.5">
							Data iniziale
						</label>
						<input
							type="date"
							value={startDate}
							onChange={(e) => setStartDate(e.target.value)}
							className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-3 text-sm text-slate-800 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-400/20 transition"
						/>
						<p className="text-slate-400 text-xs mt-1">Deve essere un lunedì.</p>
					</div>

					<div>
						<label className="text-slate-500 text-xs font-semibold uppercase tracking-wider block mb-1.5">
							Data finale
						</label>
						<input
							type="date"
							value={endDate}
							onChange={(e) => setEndDate(e.target.value)}
							className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-3 text-sm text-slate-800 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-400/20 transition"
						/>
						<p className="text-slate-400 text-xs mt-1">Deve essere una domenica.</p>
					</div>

					{(localError || submitError) && (
						<div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
							{localError || submitError}
						</div>
					)}
				</div>

				<DialogFooter className="px-5 py-4 border-t border-slate-100 flex gap-2 mx-0 mb-0">
					<Button variant="outline" onClick={onClose} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm">Annulla</Button>
					<Button onClick={handleSubmit} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm bg-indigo-500 hover:bg-indigo-400 text-white disabled:opacity-70">
						{submitting ? 'Copia in corso...' : 'Copia settimana'}
					</Button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}
