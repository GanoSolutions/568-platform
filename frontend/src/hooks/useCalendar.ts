// useCalendar.ts
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { settingsApi, shiftApi, type ShiftCopyWeekResult, type ShiftDTO } from '@/lib/apiClient';
import { getAppHubConnection } from '@/lib/realtime';
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

/**
 * Ultima settimana visualizzata, ricordata a livello di modulo così sopravvive
 * allo smontaggio di Calendar quando si naviga su un'altra pagina e si torna
 * indietro (issue #53). Un refresh della pagina ricarica il modulo da zero e
 * quindi la resetta volutamente: nessuna persistenza in sessionStorage/localStorage.
 */
let rememberedMonday: Date | null = null;

/**
 * Closed days per settimana (chiave "start_end"), condivisa a livello di modulo:
 * cambiano di rado (li tocca solo un admin) e rimontare Calendar sulla stessa
 * settimana (es. tornandoci da un'altra pagina) non deve rifare la stessa GET
 * /settings/closed-days (segnalato da Hermann in review su PR #49). Gli shift
 * invece cambiano spesso e restano sempre fetchati al mount, senza cache.
 * ponytail: nessuna invalidazione perché oggi non c'è UI per modificare i
 * closed days dal frontend — se arriva, va svuotata la voce toccata qui.
 */
const closedDaysCache = new Map<string, Set<string>>();

export function useCalendar() {
	const [currentMonday, setCurrentMonday] = useState(() => rememberedMonday ?? getMondayOfWeek(new Date()));
	const [shifts, setShifts] = useState<ShiftsMap>({});
	const [closedDays, setClosedDays] = useState<Set<string>>(new Set());
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState('');

	useEffect(() => {
		rememberedMonday = currentMonday;
	}, [currentMonday]);

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

	const goToToday = () => setCurrentMonday(getMondayOfWeek(new Date()));
	const isCurrentWeek = formatDateKey(currentMonday) === formatDateKey(getMondayOfWeek(new Date()));

	// Contatore di richiesta: se si cambia settimana più volte prima che una
	// fetch precedente sia tornata, questo scarta il risultato di quella
	// obsoleta invece di lasciarla sovrascrivere lo stato con dati di una
	// settimana che non è più quella visualizzata.
	const requestIdRef = useRef(0);

	const fetchShifts = useCallback(
		() => shiftApi.getByDateRange(weekRange.start, weekRange.end),
		[weekRange.start, weekRange.end],
	);

	const fetchClosedDays = useCallback(async () => {
		const cacheKey = `${weekRange.start}_${weekRange.end}`;
		const cached = closedDaysCache.get(cacheKey);
		if (cached) return cached;

		const dtos = await settingsApi.getClosedDaysByDateRange(weekRange.start, weekRange.end);
		const set = new Set(dtos.map(d => d.date));
		closedDaysCache.set(cacheKey, set);
		return set;
	}, [weekRange.start, weekRange.end]);

	const reloadShifts = useCallback(async ({ silent = false }: { silent?: boolean } = {}) => {
		const requestId = ++requestIdRef.current;
		if (!silent) setLoading(true);
		setError('');

		try {
			const dtos = await fetchShifts();
			if (requestId !== requestIdRef.current) return;
			setShifts(groupShiftsByDate(await dtos));
		} catch (loadError) {
			if (requestId !== requestIdRef.current) return;
			setError(loadError instanceof Error ? loadError.message : 'Caricamento calendario non riuscito');
		} finally {
			if (requestId === requestIdRef.current && !silent) setLoading(false);
		}
	}, [fetchShifts]);

	const reloadCalendar = useCallback(async ({ silent = false }: { silent?: boolean } = {}) => {
		const requestId = ++requestIdRef.current;
		if (!silent) setLoading(true);
		setError('');

		try {
			const [dtos, closedDaySet] = await Promise.all([fetchShifts(), fetchClosedDays()]);
			if (requestId !== requestIdRef.current) return;
			setShifts(groupShiftsByDate(dtos));
			setClosedDays(closedDaySet);
		} catch (loadError) {
			if (requestId !== requestIdRef.current) return;
			setError(loadError instanceof Error ? loadError.message : 'Caricamento calendario non riuscito');
		} finally {
			if (requestId === requestIdRef.current && !silent) setLoading(false);
		}
	}, [fetchShifts, fetchClosedDays]);

	useEffect(() => {
		reloadCalendar();
	}, [reloadCalendar]);

	// Canale live: la connessione è di proprietà di RequestsContext (avvio/stop),
	// qui ci si limita a sottoscrivere/annullare la sottoscrizione all'evento.
	// ShiftsChanged porta la data del turno modificato: rifacciamo il fetch degli
	// shift solo se ricade nella settimana visualizzata (altrimenti nulla è cambiato
	// nel range che stiamo mostrando). I closed days non sono toccati da questo
	// evento, quindi non li rifetchiamo qui: restano quelli caricati con la settimana.
	useEffect(() => {
		const connection = getAppHubConnection();
		connection.on('ShiftsChanged', (payload: { date: string }) => {
			if (payload.date >= weekRange.start && payload.date <= weekRange.end) {
				reloadShifts({ silent: true });
			}
		});
		return () => {
			connection.off('ShiftsChanged');
		};
	}, [reloadShifts, weekRange.start, weekRange.end]);

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
	 * scelto. L'operazione (lettura della settimana sorgente, validazione,
	 * sovrascrittura) è tutta lato backend (`POST /shift/copy-week`, atomica);
	 * qui si passa solo il lunedì visualizzato come sorgente e si ricarica la
	 * settimana corrente dopo, per riflettere eventuali turni sovrascritti.
	 */
	const copyWeek = async ({ startDate, endDate }: { startDate: string; endDate: string }): Promise<ShiftCopyWeekResult> => {
		setError('');
		const result = await shiftApi.copyWeek({
			sourceWeekMonday: formatDateKey(currentMonday),
			targetStartDate: startDate,
			targetEndDate: endDate,
		});
		await reloadCalendar();
		return result;
	};

	return {
		weekDays,
		currentMonday,
		goToPrevWeek,
		goToNextWeek,
		goToToday,
		isCurrentWeek,
		getShiftForDay,
		saveShift,
		copyWeek,
		loading,
		error,
		reloadCalendar,
	};
}
