import { Fragment } from 'react';
import { formatTimeLabel } from '@/lib/shiftTime';
import type { AppUser, Employee, ShiftData } from '@/types';

const DAYS = ['Dom', 'Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab'];

interface CalendarGridProps {
	weekDays: Date[]
	employees: Employee[]
	getShiftForDay: (date: Date) => ShiftData | null
	currentUser: AppUser | null
	pendingShiftIds: Set<string>
	onPressDay: (date: Date) => void
}

function isDayClickable(shift: ShiftData | null, currentUser: AppUser | null): boolean {
	if (currentUser?.role === 'admin') return true;
	return currentUser?.role === 'employee' && (shift?.employees?.some(e => e.id === currentUser.id) ?? false);
}

export default function CalendarGrid({ weekDays, employees, getShiftForDay, currentUser, pendingShiftIds, onPressDay }: CalendarGridProps) {
	const today = new Date();

	return (
		<div className="overflow-auto bg-white">
			<div
				className="grid"
				style={{ gridTemplateColumns: `64px repeat(${employees.length}, minmax(88px, 1fr))` }}
			>
				{/* Angolo in alto a sinistra: sopra sia l'header dipendenti che la colonna giorni */}
				<div className="sticky top-0 left-0 z-30 bg-white border-b border-r border-slate-100" />

				{/* Header: un dipendente per colonna */}
				{employees.map((emp) => {
					const isMe = emp.id === currentUser?.id;
					return (
						<div
							key={emp.id}
							className={`sticky top-0 z-20 border-b border-slate-100 px-1 py-2 text-center ${isMe ? 'bg-indigo-50' : 'bg-white'}`}
						>
							<span className="w-2.5 h-2.5 rounded-full inline-block mb-1" style={{ backgroundColor: emp.color }} />
							<span className="block text-xs font-medium text-slate-700 truncate">{emp.name}</span>
						</div>
					);
				})}

				{/* Righe: un giorno per riga */}
				{weekDays.map((day) => {
					const shift = getShiftForDay(day);
					const isClosed = shift?.closed === true;
					const isToday = day.toDateString() === today.toDateString();
					const clickable = isDayClickable(shift, currentUser);
					const dayBg = isClosed ? 'bg-slate-100' : isToday ? 'bg-indigo-50' : 'bg-white';

					return (
						<Fragment key={day.toISOString()}>
							{/* Etichetta giorno, fissa a sinistra */}
							<div
								onClick={clickable ? () => onPressDay(day) : undefined}
								className={`sticky left-0 z-10 border-r border-b border-slate-100 py-2 text-center ${dayBg} ${clickable ? 'cursor-pointer' : ''}`}
							>
								<span className={`block text-[10px] font-semibold uppercase tracking-wider ${isToday ? 'text-indigo-500' : 'text-slate-500'}`}>
									{DAYS[day.getDay()]}
								</span>
								<span className={`block text-base font-bold leading-tight ${isToday ? 'text-indigo-600' : 'text-slate-700'}`}>
									{day.getDate()}
								</span>
							</div>

							{employees.map((emp) => {
								const shiftEmp = shift?.employees?.find(e => e.id === emp.id);
								const isPending = shiftEmp ? pendingShiftIds.has(shiftEmp.shiftId) : false;

								return (
									<div
										key={emp.id}
										onClick={clickable ? () => onPressDay(day) : undefined}
										className={[
											'border-b border-slate-50 flex items-center justify-center px-1 py-2 min-h-[52px]',
											isClosed ? 'bg-slate-50' : isToday ? 'bg-indigo-50/30' : '',
											clickable ? 'cursor-pointer active:bg-indigo-50/60' : '',
										].join(' ')}
									>
										{shiftEmp && (
											<span
												className="relative inline-flex items-center justify-center rounded-lg px-2 py-1 text-[11px] font-semibold whitespace-nowrap text-slate-700"
												style={{ backgroundColor: emp.color + '1a' }}
											>
												{formatTimeLabel(shiftEmp.startTime, shiftEmp.endTime)}
												{isPending && (
													<span className="absolute -top-1 -right-1 w-2 h-2 rounded-full bg-amber-500" />
												)}
											</span>
										)}
									</div>
								);
							})}
						</Fragment>
					);
				})}
			</div>
		</div>
	);
}
