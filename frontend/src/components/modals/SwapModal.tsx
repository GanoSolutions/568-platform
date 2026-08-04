import { useMemo, useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { formatTimeLabel } from '@/lib/shiftTime';
import type { AppUser, Employee, ShiftData, SwapRequest } from '@/types';

const DAYS_FULL = ['Domenica', 'Lunedì', 'Martedì', 'Mercoledì', 'Giovedì', 'Venerdì', 'Sabato'];
const MONTHS_FULL = ['gennaio', 'febbraio', 'marzo', 'aprile', 'maggio', 'giugno', 'luglio', 'agosto', 'settembre', 'ottobre', 'novembre', 'dicembre'];

function isPastDay(date: Date): boolean {
	const today = new Date();
	today.setHours(0, 0, 0, 0);
	const day = new Date(date);
	day.setHours(0, 0, 0, 0);
	return day < today;
}

interface SwapModalProps {
	date: Date
	shift: ShiftData | null
	employees: Employee[]
	currentUser: AppUser
	/** Richieste pendenti (di chiunque), usate per non far selezionare un collega già interpellato per questo turno. */
	pendingRequests: SwapRequest[]
	onClose: () => void
	onSubmit: (payload: { date: Date; shiftId: string | undefined; targetEmployeeIds: string[]; shiftType: string; formattedDate: string }) => Promise<void>
	submitError: string
}

export default function SwapModal({ date, shift, employees, currentUser, pendingRequests, onClose, onSubmit, submitError }: SwapModalProps) {
	const [selectedColleagues, setSelectedColleagues] = useState<Set<string>>(new Set());
	const [sent, setSent] = useState(false);
	const [submitting, setSubmitting] = useState(false);

	const myShift = shift?.employees?.find(e => e.id === currentUser.id);
	const pastShift = isPastDay(date);

	// Colleghi che hanno già una richiesta pendente per questo turno: non richiedibili di nuovo.
	const alreadyRequestedIds = useMemo(
		() => new Set(pendingRequests.filter(r => r.shiftId === myShift?.shiftId).map(r => r.targetEmployeeId)),
		[pendingRequests, myShift?.shiftId]
	);

	// Solo colleghi NON già in turno quel giorno
	const availableColleagues = employees.filter(emp =>
		emp.id !== currentUser.id &&
		!shift?.employees?.some(e => e.id === emp.id)
	);

	const dayLabel = `${DAYS_FULL[date.getDay()]} ${date.getDate()} ${MONTHS_FULL[date.getMonth()]}`;
	const formattedDate = `${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}/${date.getFullYear()}`;

	const toggleColleague = (id: string) => {
		if (alreadyRequestedIds.has(id)) return;
		setSelectedColleagues(prev => {
			const next = new Set(prev);
			if (next.has(id)) next.delete(id);
			else next.add(id);
			return next;
		});
	};

	const handleSend = async () => {
		setSubmitting(true);
		try {
			await onSubmit({
				date,
				shiftId: myShift?.shiftId,
				targetEmployeeIds: [...selectedColleagues],
				shiftType: myShift ? formatTimeLabel(myShift.startTime, myShift.endTime) : '',
				formattedDate,
			});
			setSent(true);
		} finally {
			setSubmitting(false);
		}
	};

	return (
		<Dialog open onOpenChange={onClose}>
			<DialogContent className="max-w-sm w-[92vw] rounded-2xl p-0 overflow-hidden gap-0">
				<DialogHeader className="px-5 pt-5 pb-3 border-b border-slate-100">
					<DialogTitle className="text-slate-800">Richiedi cambio turno</DialogTitle>
				</DialogHeader>

				{sent ? (
					<div className="flex flex-col items-center text-center px-5 py-8">
						<div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mb-4">
							<svg className="w-7 h-7 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
								<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
							</svg>
						</div>
						<p className="font-semibold text-slate-800 text-base">Richiesta inviata!</p>
						<p className="text-sm text-slate-500 mt-1">
							{selectedColleagues.size === 1
								? `${employees.find(e => selectedColleagues.has(e.id))?.name} riceverà una notifica.`
								: `${selectedColleagues.size} colleghi riceveranno una notifica.`
							}
						</p>
					</div>
				) : (
					<div className="px-5 pt-3 pb-4 space-y-3 max-h-[60vh] overflow-y-auto">
						{/* Riepilogo turno */}
						<div className="bg-slate-50 rounded-xl px-4 py-3 space-y-1">
							<div className="flex justify-between text-sm">
								<span className="text-slate-400">Giorno</span>
								<span className="font-semibold text-slate-800 capitalize">{dayLabel}</span>
							</div>
							<div className="flex justify-between text-sm">
								<span className="text-slate-400">Turno</span>
								<span className="font-semibold text-slate-800">
									{myShift ? formatTimeLabel(myShift.startTime, myShift.endTime) : ''}
								</span>
							</div>
						</div>

						{pastShift ? (
							<p className="text-amber-700 text-sm text-center bg-amber-50 border border-amber-200 rounded-xl py-4 px-3">
								Non puoi richiedere il cambio di un turno già passato.
							</p>
						) : (
							<>
								{/* Lista colleghi disponibili */}
								<p className="text-[11px] text-slate-400 font-semibold uppercase tracking-wider px-1 pt-1">
									Scegli uno o più colleghi
								</p>
								{availableColleagues.length === 0 ? (
									<p className="text-slate-400 text-sm text-center py-6">
										Nessun collega disponibile per questo giorno
									</p>
								) : (
									availableColleagues.map(emp => {
										const isSelected = selectedColleagues.has(emp.id);
										const alreadyRequested = alreadyRequestedIds.has(emp.id);
										return (
											<div
												key={emp.id}
												onClick={() => toggleColleague(emp.id)}
												className={`flex items-center gap-3 px-4 py-3 rounded-xl border-2 transition ${alreadyRequested ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'}`}
												style={isSelected
													? { borderColor: emp.color + '60', backgroundColor: emp.color + '12' }
													: { borderColor: '#e2e8f0', backgroundColor: 'white' }
												}
											>
												<div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: emp.color }} />
												<span className="text-sm font-medium text-slate-700">{emp.name}</span>
												{alreadyRequested ? (
													<span className="ml-auto text-[10px] font-semibold text-slate-500 bg-slate-100 border border-slate-200 px-2 py-0.5 rounded-full shrink-0">
														Richiesta inviata
													</span>
												) : (
													<div
														className="w-5 h-5 ml-auto rounded flex items-center justify-center shrink-0 border-2 transition"
														style={isSelected
															? { backgroundColor: emp.color, borderColor: emp.color }
															: { backgroundColor: 'white', borderColor: '#cbd5e1' }
														}
													>
														{isSelected && (
															<svg className="w-3 h-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
																<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M5 13l4 4L19 7" />
															</svg>
														)}
													</div>
												)}
											</div>
										);
									})
								)}

								{submitError && (
									<div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
										{submitError}
									</div>
								)}
							</>
						)}
					</div>
				)}

				<DialogFooter className="px-5 py-4 border-t border-slate-100 flex gap-2 mx-0 mb-0">
					{sent ? (
						<Button onClick={onClose} className="w-full rounded-xl py-3 text-sm bg-indigo-500 hover:bg-indigo-400 text-white">
							Chiudi
						</Button>
					) : (
						<>
							<Button variant="outline" onClick={onClose} className="flex-1 rounded-xl py-3 text-sm">Annulla</Button>
							{!pastShift && (
								<Button
									onClick={handleSend}
									disabled={selectedColleagues.size === 0 || submitting}
									className="flex-1 rounded-xl py-3 text-sm bg-indigo-500 hover:bg-indigo-400 text-white disabled:opacity-70"
								>
									{submitting ? 'Invio...' : 'Conferma'}
								</Button>
							)}
						</>
					)}
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}
