import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import type { Employee, ShiftData } from '@/types';

const DAYS_FULL = ['Domenica', 'Lunedì', 'Martedì', 'Mercoledì', 'Giovedì', 'Venerdì', 'Sabato'];
const MONTHS_SHORT = ['gen', 'feb', 'mar', 'apr', 'mag', 'giu', 'lug', 'ago', 'set', 'ott', 'nov', 'dic'];

interface ShiftModalProps {
	date: Date
	shift: ShiftData | null
	employees: Employee[]
	onSave: (data: ShiftData) => Promise<void>
	onClose: () => void
	saveError: string
}

export default function ShiftModal({ date, shift, employees, onSave, onClose, saveError }: ShiftModalProps) {
	const [closed, setClosed] = useState(shift?.closed ?? false);
	const [selected, setSelected] = useState<Record<string, { partial: boolean }>>(() => {
		const map: Record<string, { partial: boolean }> = {};
		shift?.employees?.forEach(e => { map[e.id] = { partial: e.partial ?? false }; });
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
			return { ...prev, [id]: { partial: false } };
		});
	};

	const togglePartial = (id: string, e: React.MouseEvent) => {
		e.stopPropagation();
		setSelected(prev => ({
			...prev,
			[id]: { ...prev[id], partial: !prev[id]?.partial },
		}));
	};

	const handleClosed = () => {
		setClosed(c => !c);
		setSelected({});
	};

	const handleSave = async () => {
		setSubmitting(true);
		try {
			if (closed) {
				await onSave({ closed: true, employees: [] });
			} else {
				const employeeList = Object.entries(selected).map(([id, v]) => ({ id, partial: v.partial }));
				await onSave({ closed: false, employees: employeeList });
			}
		} finally {
			setSubmitting(false);
		}
	};

	const canSave = closed || Object.keys(selected).length > 0;
	const dayLabel = `${DAYS_FULL[date.getDay()]} ${date.getDate()} ${MONTHS_SHORT[date.getMonth()]}`;

	return (
		<Dialog open onOpenChange={onClose}>
			<DialogContent className="max-w-sm w-[92vw] rounded-2xl p-0 overflow-hidden gap-0">
				<DialogHeader className="px-5 pt-5 pb-4 border-b border-slate-100">
					<DialogTitle className="capitalize text-slate-800">{dayLabel}</DialogTitle>
				</DialogHeader>

				<div className="px-5 py-4 space-y-3 max-h-[60vh] overflow-y-auto">
					{/* Giornata di chiusura */}
					<button
						onClick={handleClosed}
						className={`w-full flex items-center justify-between px-4 py-3 rounded-xl border-2 transition ${closed ? 'border-slate-400 bg-slate-100' : 'border-slate-200 bg-white hover:border-slate-300'
						}`}
					>
						<span className="text-sm font-medium text-slate-700">Giornata di chiusura</span>
						<Checkbox checked={closed} color="#64748b" />
					</button>

					{/* Lista dipendenti */}
					{!closed && (
						<div className="space-y-2">
							<p className="text-[11px] text-slate-400 font-semibold uppercase tracking-wider px-1 pt-1">
								Dipendenti in turno
							</p>
							{employees.map((emp) => {
								const isSelected = selected[emp.id] !== undefined;
								const isPartial = !!selected[emp.id]?.partial;
								return (
									<div
										key={emp.id}
										onClick={() => toggleEmployee(emp.id)}
										className="flex items-center justify-between px-4 py-3 rounded-xl border-2 cursor-pointer transition"
										style={isSelected
											? { borderColor: emp.color + '60', backgroundColor: emp.color + '12' }
											: { borderColor: '#e2e8f0', backgroundColor: 'white' }
										}
									>
										<div className="flex items-center gap-3">
											<div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: emp.color }} />
											<span className="text-sm font-medium text-slate-700">{emp.name}</span>
										</div>
										<div className="flex items-center gap-2">
											{isSelected && (
												<button
													onClick={(e) => togglePartial(emp.id, e)}
													className={`text-xs px-2 py-0.5 rounded-md font-bold transition ${isPartial ? 'bg-amber-100 text-amber-700' : 'bg-slate-100 text-slate-400'
													}`}
												>
													½
												</button>
											)}
											<Checkbox checked={isSelected} color={emp.color} />
										</div>
									</div>
								);
							})}
						</div>
					)}

					{saveError && (
						<div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
							{saveError}
						</div>
					)}
				</div>

				<DialogFooter className="px-5 py-4 border-t border-slate-100 flex gap-2 mx-0 mb-0">
					<Button variant="outline" onClick={onClose} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm">Annulla</Button>
					<Button
						onClick={handleSave}
						disabled={!canSave || submitting}
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
