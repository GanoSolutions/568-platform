import EmployeeBadge from './EmployeeBadge';
import { formatTimeLabel } from '@/lib/shiftTime';
import type { AppUser, Employee, ShiftData } from '@/types';

const DAYS = ['Dom', 'Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab'];

interface DayRowProps {
	date: Date
	shift: ShiftData | null
	employees: Employee[]
	onPress: () => void
	currentUser: AppUser | null
	hasPendingRequest: boolean
}

export default function DayRow({ date, shift, employees, onPress, currentUser, hasPendingRequest }: DayRowProps) {
	const today = new Date();
	const isToday = date.toDateString() === today.toDateString();
	const isClosed = shift?.closed === true;
	const hasEmployees = shift && !shift.closed && shift.employees?.length > 0;

	const isCurrentUserInShift = currentUser?.role === 'employee' &&
		shift?.employees?.some(e => e.id === currentUser.id);

	const clickable = currentUser?.role === 'admin' || isCurrentUserInShift;

	const dayName = DAYS[date.getDay()];
	const dayNum = date.getDate();

	return (
		<div
			onClick={clickable ? onPress : undefined}
			className={[
				'flex items-stretch min-h-[72px]',
				isClosed ? 'bg-slate-100' : 'bg-white',
				clickable ? 'cursor-pointer active:bg-indigo-50/50' : '',
				isToday ? 'border-l-4 border-indigo-500' : 'border-l-4 border-transparent',
			].join(' ')}
		>
			{/* Etichetta giorno */}
			<div className={`w-14 flex flex-col items-center justify-center py-3 shrink-0 ${isClosed ? 'opacity-40' : ''}`}>
				<span className={`text-[11px] font-semibold uppercase tracking-wider ${isToday ? 'text-indigo-500' : 'text-slate-500'}`}>
					{dayName}
				</span>
				<span className={`text-2xl font-bold leading-tight mt-0.5 ${isToday ? 'text-indigo-600' : 'text-slate-700'}`}>
					{dayNum}
				</span>
			</div>

			{/* Separatore verticale */}
			<div className="w-px bg-slate-100 my-2 shrink-0" />

			{/* Contenuto */}
			<div className="flex-1 flex flex-wrap content-center gap-1.5 px-3 py-3">
				{hasPendingRequest && (
					<span className="inline-flex items-center gap-1 rounded-full bg-amber-50 border border-amber-200 px-2 py-1 text-[11px] font-semibold text-amber-700">
						<span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
						Richiesta pendente
					</span>
				)}
				{isClosed ? (
					<span className="text-slate-400 text-sm font-medium">Chiuso</span>
				) : !hasEmployees ? (
					currentUser?.role === 'admin' ? (
						<span className="text-slate-500 text-sm">+ Crea turno</span>
					) : (
						<span className="text-slate-500 text-sm italic">Nessun turno</span>
					)
				) : (
					shift.employees.map((shiftEmp) => {
						const emp = employees.find(e => e.id === shiftEmp.id);
						if (!emp) return null;
						return (
							<EmployeeBadge
								key={shiftEmp.id}
								name={emp.name}
								color={emp.color}
								timeLabel={formatTimeLabel(shiftEmp.startTime, shiftEmp.endTime, shiftEmp.overnight)}
								isCurrentUser={emp.id === currentUser?.id}
							/>
						);
					})
				)}
			</div>

			{/* Freccia per righe cliccabili */}
			{clickable && (
				<div className="flex items-center pr-3 shrink-0">
					<svg className="w-4 h-4 text-slate-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
					</svg>
				</div>
			)}
		</div>
	);
}
