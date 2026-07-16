import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import type { Employee, ShiftData } from '@/types';

const DAYS_FULL = ['Domenica', 'Lunedì', 'Martedì', 'Mercoledì', 'Giovedì', 'Venerdì', 'Sabato'];
const MONTHS_FULL = ['gennaio', 'febbraio', 'marzo', 'aprile', 'maggio', 'giugno', 'luglio', 'agosto', 'settembre', 'ottobre', 'novembre', 'dicembre'];

// Convenzione turno pieno usata nei dati di esempio: 18:00 -> 00:00 (6h).
const DEFAULT_START_TIME = '18:00';
const DEFAULT_END_TIME = '00:00';

interface ShiftTimes {
	startTime: string
	endTime: string
}

interface ShiftModalProps {
	date: Date
	shift: ShiftData | null
	employees: Employee[]
	onSave: (entries: { employeeId: string; startTime: string; endTime: string }[]) => Promise<void>
	onClose: () => void
	saveError: string
}

export default function ShiftModal({ date, shift, employees, onSave, onClose, saveError }: ShiftModalProps) {
	const [selected, setSelected] = useState<Record<string, ShiftTimes>>(() => {
		const map: Record<string, ShiftTimes> = {};
		shift?.employees?.forEach(e => { map[e.id] = { startTime: e.startTime, endTime: e.endTime }; });
		return map;
	});
	const [submitting, setSubmitting] = useState(false);

	const toggleEmployee = (id: string) => {
		setSelected(prev => {
			if (prev[id] !== undefined) {
				const next = { ...prev };
				delete next[id];
				return next;
			}
			return { ...prev, [id]: { startTime: DEFAULT_START_TIME, endTime: DEFAULT_END_TIME } };
		});
	};

	const updateTime = (id: string, field: keyof ShiftTimes, value: string) => {
		setSelected(prev => ({ ...prev, [id]: { ...prev[id], [field]: value } }));
	};

	const handleSave = async () => {
		setSubmitting(true);
		try {
			const entries = Object.entries(selected).map(([employeeId, times]) => ({ employeeId, ...times }));
			await onSave(entries);
		} finally {
			setSubmitting(false);
		}
	};

	const dayLabel = `${DAYS_FULL[date.getDay()]} ${date.getDate()} ${MONTHS_FULL[date.getMonth()]}`;

	return (
		<Dialog open onOpenChange={onClose}>
			<DialogContent className="max-w-sm w-[92vw] rounded-2xl p-0 overflow-hidden gap-0">
				<DialogHeader className="px-5 pt-5 pb-3 border-b border-slate-100">
					<DialogTitle className="capitalize text-slate-800">{dayLabel}</DialogTitle>
				</DialogHeader>

				<div className="px-5 pt-3 pb-4 space-y-3 max-h-[60vh] overflow-y-auto">
					<div className="space-y-2">
						<p className="text-[11px] text-slate-400 font-semibold uppercase tracking-wider px-1 pt-1">
							Dipendenti in turno
						</p>
						{employees.map((emp) => {
							const isSelected = selected[emp.id] !== undefined;
							const times = selected[emp.id];
							return (
								<div
									key={emp.id}
									className="rounded-xl border-2 transition"
									style={isSelected
										? { borderColor: emp.color + '60', backgroundColor: emp.color + '12' }
										: { borderColor: '#e2e8f0', backgroundColor: 'white' }
									}
								>
									<div
										onClick={() => toggleEmployee(emp.id)}
										className="flex items-center justify-between px-4 py-3 cursor-pointer"
									>
										<div className="flex items-center gap-3">
											<div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: emp.color }} />
											<span className="text-sm font-medium text-slate-700">{emp.name}</span>
										</div>
										<Checkbox checked={isSelected} color={emp.color} />
									</div>
									{isSelected && times && (
										<div className="flex items-center gap-2 px-4 pb-3">
											<input
												type="time"
												value={times.startTime}
												onClick={(e) => e.stopPropagation()}
												onChange={(e) => updateTime(emp.id, 'startTime', e.target.value)}
												className="flex-1 bg-white border border-slate-200 rounded-lg px-2 py-1.5 text-sm text-slate-800 outline-none focus:border-indigo-400"
											/>
											<span className="text-slate-400 text-sm">–</span>
											<input
												type="time"
												value={times.endTime}
												onClick={(e) => e.stopPropagation()}
												onChange={(e) => updateTime(emp.id, 'endTime', e.target.value)}
												className="flex-1 bg-white border border-slate-200 rounded-lg px-2 py-1.5 text-sm text-slate-800 outline-none focus:border-indigo-400"
											/>
										</div>
									)}
								</div>
							);
						})}
					</div>

					{saveError && (
						<div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 whitespace-pre-line">
							{saveError}
						</div>
					)}
				</div>

				<DialogFooter className="px-5 py-4 border-t border-slate-100 flex gap-2">
					<Button variant="outline" onClick={onClose} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm">Annulla</Button>
					<Button
						onClick={handleSave}
						disabled={submitting}
						className="flex-1 rounded-xl py-3 text-sm bg-indigo-500 hover:bg-indigo-400 text-white disabled:opacity-70"
					>
						{submitting ? 'Salvataggio...' : 'Conferma'}
					</Button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}

function Checkbox({ checked, color }: { checked: boolean; color: string }) {
	return (
		<div
			className="w-5 h-5 rounded-md border-2 flex items-center justify-center shrink-0 transition"
			style={checked ? { backgroundColor: color, borderColor: color } : { borderColor: '#d1d5db' }}
		>
			{checked && (
				<svg className="w-3 h-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
					<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M5 13l4 4L19 7" />
				</svg>
			)}
		</div>
	);
}
