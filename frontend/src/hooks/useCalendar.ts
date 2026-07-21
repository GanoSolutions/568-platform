// useCalendar.ts
import { useCallback, useEffect, useMemo, useState } from 'react';
import { settingsApi, shiftApi, type ShiftDTO } from '@/lib/apiClient';
import { computeDuration, toBackendTime, toDisplayRange } from '@/lib/shiftTime';
import type { ShiftData, ShiftEmployee } from '@/types';

type ShiftsMap = Record<string, ShiftData>

function getMondayOfWeek(date: Date): Date {
	const d = new Date(date);
	const day = d.getDay();
	const diff = day === 0 ? -6 : 1 - day;
	d.setDate(d.getDate() + diff);
	d.setHours(0, 0, 0, 0);
	return d;
}

function addDays(date: Date, amount: number): Date {
	const next = new Date(date);
	next.setDate(next.getDate() + amount);
	return next;
}

export function formatDateKey(date: Date): string {
	const y = date.getFullYear();
	const m = String(date.getMonth() + 1).padStart(2, '0');
	const d = String(date.getDate()).padStart(2, '0');
	return `${y}-${m}-${d}`;
}

export function getWeekNumber(date: Date): number {
	const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
	const dayNum = d.getUTCDay() || 7;
	d.setUTCDate(d.getUTCDate() + 4 - dayNum);
	const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
	return Math.ceil(((d.getTime() - yearStart.getTime()) / 86400000 + 1) / 7);
}

function getWeekRange(monday: Date): { start: string; end: string } {
	const start = formatDateKey(monday);
	const endDate = new Date(monday);
	endDate.setDate(endDate.getDate() + 6);
	const end = formatDateKey(endDate);
	return { start, end };
}

function groupShiftsByDate(dtos: ShiftDTO[]): ShiftsMap {
	return dtos.reduce<ShiftsMap>((acc, dto) => {
		const { startTime, endTime, overnight } = toDisplayRange(dto.startTime, dto.duration);
		const entry: ShiftEmployee = { id: dto.employeeId, shiftId: dto.id, startTime, endTime, overnight };
		if (!acc[dto.date]) acc[dto.date] = { closed: false, employees: [] };
		acc[dto.date].employees.push(entry);
		return acc;
	}, {});
}

export function useCalendar() {
	const [currentMonday, setCurrentMonday] = useState(() => getMondayOfWeek(new Date()));
	const [shifts, setShifts] = useState<ShiftsMap>({});
	const [closedDays, setClosedDays] = useState<Set<string>>(new Set());
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState('');

	const weekDays = Array.from({ length: 7 }, (_, i) => {
		const d = new Date(currentMonday);
		d.setDate(currentMonday.getDate() + i);
		return d;
	});

	const weekRange = useMemo(() => getWeekRange(currentMonday), [currentMonday]);

	const goToPrevWeek = () => setCurrentMonday(prev => {
		const d = new Date(prev); d.setDate(d.getDate() - 7); return d;
	});

	const goToNextWeek = () => setCurrentMonday(prev => {
		const d = new Date(prev); d.setDate(d.getDate() + 7); return d;
	});

	const reloadCalendar = useCallback(async () => {
		setLoading(true);
		setError('');

		try {
			const [dtos, closedDayDtos] = await Promise.all([
				shiftApi.getByDateRange(weekRange.start, weekRange.end),
				settingsApi.getClosedDaysByDateRange(weekRange.start, weekRange.end),
			]);
			setShifts(groupShiftsByDate(dtos));
			setClosedDays(new Set(closedDayDtos.map(d => d.date)));
		} catch (loadError) {
			setError(loadError instanceof Error ? loadError.message : 'Caricamento calendario non riuscito');
		} finally {
			setLoading(false);
		}
	}, [weekRange.start, weekRange.end]);

	useEffect(() => {
		reloadCalendar();
	}, [reloadCalendar]);

	const getShiftForDay = (date: Date): ShiftData | null => {
		const key = formatDateKey(date);
		const closed = closedDays.has(key);
		if (shifts[key]) return { ...shifts[key], closed };
		if (closed) return { closed: true, employees: [] };
		return null;
	};

	/**
	 * Applica il nuovo elenco di dipendenti/orari per un giorno confrontandolo con
	 * quello attuale: crea i nuovi turni, aggiorna quelli con orario cambiato,
	 * cancella quelli rimossi. La PUT/POST su un singolo turno restituisce già
	 * l'oggetto aggiornato, quindi lo stato locale viene patchato direttamente con
	 * quella risposta invece di rifare il fetch dell'intera settimana. La chiusura
	 * del giorno resta quella persistita lato backend (`/settings/closed-days`),
	 * non gestibile da qui.
	 */
	const saveShift = async (date: Date, desiredEmployees: { employeeId: string; startTime: string; endTime: string }[]) => {
		const key = formatDateKey(date);
		const current = shifts[key]?.employees ?? [];
		const currentByEmployeeId = new Map(current.map(e => [e.id, e]));
		const desiredByEmployeeId = new Map(desiredEmployees.map(e => [e.employeeId, e]));

		setError('');

		for (const existing of current) {
			if (!desiredByEmployeeId.has(existing.id)) {
				await shiftApi.del(existing.shiftId);
			}
		}

		const updatedEmployees: ShiftEmployee[] = [];

		for (const desired of desiredEmployees) {
			const startTime = toBackendTime(desired.startTime);
			const duration = computeDuration(desired.startTime, desired.endTime);
			const existing = currentByEmployeeId.get(desired.employeeId);

			let dto: ShiftDTO;
			if (!existing) {
				dto = await shiftApi.create({ employeeId: desired.employeeId, date: key, startTime, duration });
			} else if (existing.startTime !== desired.startTime || existing.endTime !== desired.endTime) {
				dto = await shiftApi.update(existing.shiftId, { startTime, duration });
			} else {
				updatedEmployees.push(existing);
				continue;
			}
			const displayRange = toDisplayRange(dto.startTime, dto.duration);
			updatedEmployees.push({ id: dto.employeeId, shiftId: dto.id, ...displayRange });
		}

		setShifts(prev => ({ ...prev, [key]: { closed: false, employees: updatedEmployees } }));
	};

	/**
	 * Copia i turni della settimana visibile su ogni settimana nell'intervallo
	 * scelto. Un fallimento su un singolo turno (es. 422 perché il dipendente è
	 * già assegnato quel giorno) non blocca gli altri: viene raccolto e segnalato
	 * in coda dopo aver comunque applicato tutto il resto.
	 */
	const copyWeek = async ({ startDate, endDate }: { startDate: string; endDate: string }) => {
		const sourceWeek = weekDays.map((day) => ({
			date: new Date(day),
			shift: getShiftForDay(day),
		}));

		const startMonday = getMondayOfWeek(new Date(startDate));
		const endMonday = getMondayOfWeek(new Date(endDate));

		if (endMonday < startMonday) {
			throw new Error('Periodo non valido per la copia settimana');
		}

		setError('');
		const failures: string[] = [];

		for (let monday = new Date(startMonday); monday <= endMonday; monday = addDays(monday, 7)) {
			for (let index = 0; index < sourceWeek.length; index += 1) {
				const sourceDay = sourceWeek[index];
				const targetDate = addDays(monday, index);
				const targetKey = formatDateKey(targetDate);

				if (targetKey === formatDateKey(sourceDay.date)) continue;
				if (!sourceDay.shift || sourceDay.shift.closed) continue;

				for (const employee of sourceDay.shift.employees) {
					try {
						await shiftApi.create({
							employeeId: employee.id,
							date: targetKey,
							startTime: toBackendTime(employee.startTime),
							duration: computeDuration(employee.startTime, employee.endTime),
						});
					} catch (copyError) {
						failures.push(`${targetKey}: ${copyError instanceof Error ? copyError.message : 'errore sconosciuto'}`);
					}
				}
			}
		}

		await reloadCalendar();

		if (failures.length > 0) {
			throw new Error(`Alcuni turni non sono stati copiati:\n${failures.join('\n')}`);
		}
	};

	return {
		weekDays,
		currentMonday,
		goToPrevWeek,
		goToNextWeek,
		getShiftForDay,
		saveShift,
		copyWeek,
		loading,
		error,
		reloadCalendar,
	};
}
