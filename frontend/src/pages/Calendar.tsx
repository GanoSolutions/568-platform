import { useState } from 'react';
import { useAuth } from '@/context/AuthContext';
import { useRequests } from '@/context/RequestsContext';
import { useCalendar, getWeekNumber } from '@/hooks/useCalendar';
import { useEmployees } from '@/hooks/useEmployees';
import CalendarGrid from '@/components/calendar/CalendarGrid';
import CopyWeekModal from '@/components/modals/CopyWeekModal';
import ShiftModal from '@/components/modals/ShiftModal';
import SwapModal from '@/components/modals/SwapModal';

const MONTHS = ['Gennaio', 'Febbraio', 'Marzo', 'Aprile', 'Maggio', 'Giugno',
	'Luglio', 'Agosto', 'Settembre', 'Ottobre', 'Novembre', 'Dicembre'];

export default function Calendar() {
	const { user } = useAuth();
	const { pendingShiftIds, createSwapRequest } = useRequests();
	const {
		weekDays,
		currentMonday,
		goToPrevWeek,
		goToNextWeek,
		goToToday,
		isCurrentWeek,
		getShiftForDay,
		saveShift,
		copyWeek,
		loading: calendarLoading,
		error: calendarError,
	} = useCalendar();
	const { employees: allEmployees, loading: employeesLoading, error: employeesError } = useEmployees();
	// I turni si assegnano a manager/dipendenti attivi: escludiamo gli account
	// admin puri, quelli non ancora invitati e quelli disabilitati (fine contratto).
	const employees = allEmployees.filter(e => e.role !== 'admin' && e.invited && !e.disabled);
	const loading = calendarLoading || employeesLoading;
	const [selectedDay, setSelectedDay] = useState<Date | null>(null);
	const [selectedEmployeeId, setSelectedEmployeeId] = useState<string | undefined>(undefined);
	const [copyWeekOpen, setCopyWeekOpen] = useState(false);
	const [actionError, setActionError] = useState('');

	const closeShiftModal = () => {
		setSelectedDay(null);
		setSelectedEmployeeId(undefined);
	};

	const month = MONTHS[currentMonday.getMonth()];
	const year = currentMonday.getFullYear();
	const weekNum = getWeekNumber(currentMonday);

	const selectedShift = selectedDay ? getShiftForDay(selectedDay) : null;

	const handleSaveShift = async (entries: { employeeId: string; startTime: string; endTime: string }[]) => {
		setActionError('');
		try {
			await saveShift(selectedDay!, entries);
			closeShiftModal();
		} catch (saveError) {
			setActionError((saveError as Error).message || 'Salvataggio turno non riuscito');
			throw saveError;
		}
	};

	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	const handleSwapRequest = async (payload: any) => {
		setActionError('');
		try {
			await createSwapRequest(payload);
		} catch (swapError) {
			setActionError((swapError as Error).message || 'Richiesta cambio non riuscita');
			throw swapError;
		}
	};

	const handleCopyWeek = async (payload: { startDate: string; endDate: string }) => {
		setActionError('');
		try {
			await copyWeek(payload);
			setCopyWeekOpen(false);
		} catch (copyError) {
			setActionError((copyError as Error).message || 'Copia settimana non riuscita');
			throw copyError;
		}
	};

	return (
		<div className="flex flex-col">
			{/* Navigazione settimana */}
			<div className="bg-white border-b border-slate-200 px-4 py-3 flex items-center justify-between">
				<button
					onClick={goToPrevWeek}
					className="w-9 h-9 flex items-center justify-center rounded-xl text-slate-500 hover:bg-slate-100 active:bg-slate-200 transition"
				>
					<svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
						<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
					</svg>
				</button>
				<div className="text-center">
					<p className="font-bold text-slate-800">{month} {year}</p>
					<p className="text-xs text-slate-400 mt-0.5">Settimana {weekNum}</p>
				</div>
				<div className="flex items-center gap-1">
					{!isCurrentWeek && (
						<button
							onClick={goToToday}
							className="rounded-xl px-3 h-9 text-xs font-semibold text-indigo-600 hover:bg-indigo-50 active:bg-indigo-100 transition"
						>
							Oggi
						</button>
					)}
					<button
						onClick={goToNextWeek}
						className="w-9 h-9 flex items-center justify-center rounded-xl text-slate-500 hover:bg-slate-100 active:bg-slate-200 transition"
					>
						<svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
						</svg>
					</button>
				</div>
			</div>

			{user?.role === 'admin' && (
				<div className="bg-white px-4 py-3 border-b border-slate-100 flex justify-end">
					<button
						onClick={() => setCopyWeekOpen(true)}
						className="inline-flex items-center gap-2 rounded-xl bg-indigo-50 px-4 py-2.5 text-sm font-semibold text-indigo-600 border border-indigo-200 hover:bg-indigo-100 transition"
					>
						<svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h8M8 12h8m-8 5h5M7 3h10a2 2 0 012 2v14a2 2 0 01-2 2H7a2 2 0 01-2-2V5a2 2 0 012-2z" />
						</svg>
						Copia settimana
					</button>
				</div>
			)}

			{(calendarError || employeesError || actionError) && (
				<div className="mx-4 mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
					{actionError || calendarError || employeesError}
				</div>
			)}

			{/* Griglia settimana: giorni sulle colonne, dipendenti sulle righe */}
			{loading ? (
				<div className="flex flex-col items-center text-center py-16 px-8 bg-white">
					<div className="w-8 h-8 rounded-full border-2 border-slate-200 border-t-indigo-500 animate-spin mb-4" />
					<p className="text-slate-400 text-sm">Caricamento turni...</p>
				</div>
			) : (
				<CalendarGrid
					weekDays={weekDays}
					employees={employees}
					getShiftForDay={getShiftForDay}
					currentUser={user}
					pendingShiftIds={pendingShiftIds}
					onPressDay={(day, employeeId) => { setSelectedDay(day); setSelectedEmployeeId(employeeId); }}
				/>
			)}

			{/* Modal Admin: crea / modifica turno */}
			{user?.role === 'admin' && copyWeekOpen && (
				<CopyWeekModal
					currentMonday={currentMonday}
					onSubmit={handleCopyWeek}
					onClose={() => setCopyWeekOpen(false)}
					submitError={actionError}
				/>
			)}

			{user?.role === 'admin' && selectedDay && (
				<ShiftModal
					date={selectedDay}
					shift={selectedShift}
					employees={employees}
					preselectedEmployeeId={selectedEmployeeId}
					onSave={handleSaveShift}
					onClose={closeShiftModal}
					saveError={actionError}
				/>
			)}

			{/* Modal Dipendente: richiedi cambio */}
			{user?.role === 'employee' && selectedDay && (
				<SwapModal
					date={selectedDay}
					shift={selectedShift}
					employees={employees}
					currentUser={user}
					onClose={closeShiftModal}
					onSubmit={handleSwapRequest}
					submitError={actionError}
				/>
			)}
		</div>
	);
}
