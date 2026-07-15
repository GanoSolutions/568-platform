// Conversioni fra il formato orario del backend (TimeOnly "HH:mm:ss" e
// TimeSpan ".NET" "[d.]hh:mm:ss") e le coppie startTime/endTime "HH:mm" usate in UI.

const MINUTES_PER_DAY = 24 * 60;

function parseTimeToMinutes(time: string): number {
	const [h, m] = time.split(':').map(Number);
	return h * 60 + m;
}

function formatMinutesAsTime(totalMinutes: number): string {
	const m = ((totalMinutes % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY;
	const h = Math.floor(m / 60);
	return `${String(h).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;
}

/** Parsa una TimeSpan .NET ("[d.]hh:mm:ss", es. "06:00:00" o "1.00:00:00") in minuti totali. */
export function parseDurationToMinutes(duration: string): number {
	const dotIndex = duration.indexOf('.');
	const days = dotIndex === -1 ? 0 : Number(duration.slice(0, dotIndex));
	const timePart = dotIndex === -1 ? duration : duration.slice(dotIndex + 1);
	const [h, m] = timePart.split(':').map(Number);
	return days * MINUTES_PER_DAY + h * 60 + m;
}

/** Formatta un numero di minuti (0 < n <= 1440) come TimeSpan .NET. */
export function formatMinutesAsDuration(totalMinutes: number): string {
	if (totalMinutes === MINUTES_PER_DAY) return '1.00:00:00';
	const h = Math.floor(totalMinutes / 60);
	const m = totalMinutes % 60;
	return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:00`;
}

/** startTime backend ("HH:mm:ss") + duration → coppia HH:mm per la UI, con flag overnight. */
export function toDisplayRange(startTime: string, duration: string): { startTime: string; endTime: string; overnight: boolean } {
	const startMinutes = parseTimeToMinutes(startTime);
	const endMinutesRaw = startMinutes + parseDurationToMinutes(duration);
	return {
		startTime: formatMinutesAsTime(startMinutes),
		endTime: formatMinutesAsTime(endMinutesRaw),
		overnight: endMinutesRaw >= MINUTES_PER_DAY,
	};
}

/** Coppia HH:mm inserita in UI → durata TimeSpan .NET (endTime <= startTime = sconfina a mezzanotte). */
export function computeDuration(startTime: string, endTime: string): string {
	const startMinutes = parseTimeToMinutes(startTime);
	const endMinutes = parseTimeToMinutes(endTime);
	const wrapped = endMinutes <= startMinutes ? endMinutes + MINUTES_PER_DAY : endMinutes;
	return formatMinutesAsDuration(wrapped - startMinutes);
}

/** "HH:mm" da input UI → "HH:mm:ss" per il backend. */
export function toBackendTime(time: string): string {
	return `${time}:00`;
}

/** Etichetta leggibile per un turno, es. "18:00–00:00" o "22:00–02:00 (+1)". */
export function formatTimeLabel(startTime: string, endTime: string, overnight: boolean): string {
	return overnight ? `${startTime}–${endTime} (+1)` : `${startTime}–${endTime}`;
}
