import { useMemo, useState } from 'react';
import { useAuth } from '@/context/AuthContext';
import { useRequests } from '@/context/RequestsContext';
import { useEmployees } from '@/hooks/useEmployees';
import { usePushNotifications, isWebPushConfigured } from '@/hooks/usePushNotifications';
import { isSupabaseConfigured, supabase } from '@/lib/supabase';
import type { SwapRequest } from '@/types';

const STATUS_STYLES: Record<string, string> = {
	pending: 'bg-amber-50 text-amber-700 border-amber-200',
	accepted: 'bg-green-50 text-green-700 border-green-200',
	rejected: 'bg-red-50 text-red-700 border-red-200',
	cancelled: 'bg-slate-100 text-slate-500 border-slate-200',
};

function formatDate(dateStr: string): string {
	const [year, month, day] = dateStr.split('-');
	return `${day}/${month}/${year}`;
}

export default function Requests() {
	const { user, refreshProfile } = useAuth();
	const { requests, loading, error, respondToSwapRequest, cancelSwapRequest } = useRequests();
	const { employees } = useEmployees();
	const push = usePushNotifications();
	const [actionError, setActionError] = useState('');
	const [busyId, setBusyId] = useState<string | null>(null);

	const employeesById = useMemo(() => new Map(employees.map(emp => [emp.id, emp])), [employees]);

	// Telegram linking state
	const [telegramToken, setTelegramToken] = useState<string | null>(null);
	const [telegramLinking, setTelegramLinking] = useState(false);
	const [telegramChecking, setTelegramChecking] = useState(false);
	const [telegramError, setTelegramError] = useState('');

	const handleGenerateTelegramLink = async () => {
		if (!isSupabaseConfigured || !supabase) return;
		setTelegramLinking(true);
		setTelegramError('');
		try {
			const { data, error: rpcError } = await supabase.rpc('generate_telegram_link_token');
			if (rpcError) throw rpcError;
			setTelegramToken(data as string);
		} catch (e) {
			setTelegramError((e as Error).message || 'Errore generazione codice');
		} finally {
			setTelegramLinking(false);
		}
	};

	const handleCheckTelegramLink = async () => {
		setTelegramChecking(true);
		setTelegramError('');
		try {
			await refreshProfile();
			setTelegramToken(null);
		} catch (e) {
			setTelegramError((e as Error).message || 'Errore verifica');
		} finally {
			setTelegramChecking(false);
		}
	};

	const handleUnlinkTelegram = async () => {
		if (!isSupabaseConfigured || !supabase) return;
		setTelegramLinking(true);
		setTelegramError('');
		try {
			const { error: rpcError } = await supabase.rpc('unlink_telegram');
			if (rpcError) throw rpcError;
			await refreshProfile();
		} catch (e) {
			setTelegramError((e as Error).message || 'Errore scollegamento');
		} finally {
			setTelegramLinking(false);
		}
	};

	const handleDecision = async (requestId: string, decision: string) => {
		setActionError('');
		setBusyId(requestId);
		try {
			await respondToSwapRequest(requestId, decision);
		} catch (decisionError) {
			setActionError((decisionError as Error).message || 'Aggiornamento richiesta non riuscito');
		} finally {
			setBusyId(null);
		}
	};

	const handleCancel = async (requestId: string) => {
		setActionError('');
		setBusyId(requestId);
		try {
			await cancelSwapRequest(requestId);
		} catch (cancelError) {
			setActionError((cancelError as Error).message || 'Annullamento richiesta non riuscito');
		} finally {
			setBusyId(null);
		}
	};

	return (
		<div className="flex flex-col">
			<div className="bg-white border-b border-slate-200 px-4 py-3">
				<p className="font-bold text-slate-800">Richieste cambio</p>
				<p className="text-xs text-slate-500 mt-0.5">Storico e notifiche in-app</p>
			</div>

			<div className="bg-white px-4 py-4 border-b border-slate-100">
				<div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-4 space-y-3">
					<div>
						<p className="font-semibold text-slate-800">Notifiche browser</p>
						<p className="text-sm text-slate-500 mt-1">
							Ricevi una notifica quando arriva una richiesta o quando la tua richiesta viene accettata o rifiutata.
						</p>
					</div>

					{push.availability === 'unsupported' && (
						<p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-xl px-3 py-2">
							Questo browser non supporta Web Push.
						</p>
					)}

					{push.availability === 'vapid-missing' && (
						<p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-xl px-3 py-2">
							Configura `VITE_VAPID_PUBLIC_KEY` per attivare le notifiche push.
						</p>
					)}

					{push.error && (
						<p className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-xl px-3 py-2">
							{push.error}
						</p>
					)}

					{push.availability === 'ready' && (
						<div className="flex gap-2">
							{push.enabled ? (
								<button
									onClick={push.disable}
									disabled={push.loading}
									className="flex-1 rounded-xl border border-slate-200 bg-white py-3 text-sm font-semibold text-slate-700 hover:bg-slate-100 disabled:opacity-70"
								>
									{push.loading ? 'Aggiornamento...' : 'Disattiva notifiche'}
								</button>
							) : (
								<button
									onClick={push.enable}
									disabled={push.loading}
									className="flex-1 rounded-xl border border-indigo-200 bg-indigo-50 py-3 text-sm font-semibold text-indigo-700 hover:bg-indigo-100 disabled:opacity-70"
								>
									{push.loading ? 'Attivazione...' : 'Attiva notifiche'}
								</button>
							)}
						</div>
					)}

					<p className="text-xs text-slate-500">
						Stato browser: {push.permission}. {isWebPushConfigured ? 'VAPID configurato.' : 'VAPID non configurato.'}
					</p>
				</div>
			</div>

			{/* Telegram linking */}
			<div className="bg-white px-4 py-4 border-b border-slate-100">
				<div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-4 space-y-3">
					<div className="flex items-start justify-between gap-3">
						<div>
							<p className="font-semibold text-slate-800">Notifiche Telegram</p>
							<p className="text-sm text-slate-500 mt-1">
								Collega il tuo account Telegram per ricevere notifiche istantanee su richieste e turni.
							</p>
						</div>
						<svg className="w-8 h-8 shrink-0 text-sky-500" viewBox="0 0 24 24" fill="currentColor">
							<path d="M12 0C5.373 0 0 5.373 0 12s5.373 12 12 12 12-5.373 12-12S18.627 0 12 0zm5.562 8.248-2.018 9.51c-.15.686-.543.853-1.1.53l-3.036-2.237-1.465 1.41c-.162.162-.3.3-.614.3l.219-3.1 5.639-5.093c.245-.219-.053-.34-.383-.121l-6.974 4.39-3.005-.937c-.652-.204-.665-.652.138-.964l11.73-4.524c.543-.196 1.017.12.869.836z" />
						</svg>
					</div>

					{user?.telegramLinked && !telegramToken && (
						<div className="flex items-center gap-2 text-sm text-green-700 bg-green-50 border border-green-200 rounded-xl px-3 py-2">
							<svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
								<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
							</svg>
							Account Telegram collegato
						</div>
					)}

					{telegramError && (
						<p className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-xl px-3 py-2">
							{telegramError}
						</p>
					)}

					{telegramToken && (
						<div className="space-y-2">
							<p className="text-sm text-slate-600">
								Apri il link su Telegram e tocca <strong>Start</strong>:
							</p>
							<a
								href={`https://t.me/Turni568Bot?start=${telegramToken}`}
								target="_blank"
								rel="noreferrer"
								className="block text-center rounded-xl bg-sky-50 border border-sky-200 py-3 text-sm font-semibold text-sky-700 hover:bg-sky-100"
							>
								📱 Apri @Turni568Bot
							</a>
							<button
								onClick={handleCheckTelegramLink}
								disabled={telegramChecking}
								className="w-full rounded-xl border border-slate-200 bg-white py-3 text-sm font-semibold text-slate-700 hover:bg-slate-100 disabled:opacity-70"
							>
								{telegramChecking ? 'Verifica in corso...' : '✓ Ho collegato — Verifica'}
							</button>
						</div>
					)}

					{!telegramToken && (
						<div className="flex gap-2">
							{user?.telegramLinked ? (
								<button
									onClick={handleUnlinkTelegram}
									disabled={telegramLinking}
									className="flex-1 rounded-xl border border-slate-200 bg-white py-3 text-sm font-semibold text-slate-700 hover:bg-slate-100 disabled:opacity-70"
								>
									{telegramLinking ? 'Scollegamento...' : 'Scollega Telegram'}
								</button>
							) : (
								<button
									onClick={handleGenerateTelegramLink}
									disabled={telegramLinking}
									className="flex-1 rounded-xl border border-sky-200 bg-sky-50 py-3 text-sm font-semibold text-sky-700 hover:bg-sky-100 disabled:opacity-70"
								>
									{telegramLinking ? 'Generazione link...' : 'Collega Telegram'}
								</button>
							)}
						</div>
					)}
				</div>
			</div>

			{(error || actionError) && (
				<div className="mx-4 mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
					{actionError || error}
				</div>
			)}

			{loading && (
				<div className="flex flex-col items-center text-center py-16 px-8 bg-white">
					<div className="w-8 h-8 rounded-full border-2 border-slate-200 border-t-indigo-500 animate-spin mb-4" />
					<p className="text-slate-500 text-sm">Caricamento richieste...</p>
				</div>
			)}

			{!loading && requests.length === 0 && (
				<div className="flex flex-col items-center text-center py-16 px-8 bg-white">
					<div className="w-14 h-14 bg-slate-100 rounded-full flex items-center justify-center mb-4">
						<svg className="w-7 h-7 text-slate-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-4 4v-4z" />
						</svg>
					</div>
					<p className="text-slate-500 text-sm">Nessuna richiesta presente</p>
				</div>
			)}

			{!loading && requests.length > 0 && (
				<div className="divide-y divide-slate-100">
					{requests.map((request: SwapRequest) => {
						const isIncoming = request.targetEmployeeId === user?.id;
						const isOwn = request.requesterId === user?.id;
						const canRespond = request.status === 'pending' && (isIncoming || user?.role === 'admin');
						const canCancel = request.status === 'pending' && isOwn;
						const requesterName = employeesById.get(request.requesterId)?.name ?? 'Sconosciuto';
						const targetName = employeesById.get(request.targetEmployeeId)?.name ?? 'Sconosciuto';
						const shiftChanged = Boolean(request.shiftUpdatedAt) && new Date(request.shiftUpdatedAt) > new Date(request.createdAt);

						return (
							<div key={request.id} className="bg-white px-4 py-4 space-y-3">
								<div className="flex items-start justify-between gap-3">
									<div>
										<p className="font-semibold text-slate-800">{request.workDate ? formatDate(request.workDate) : '—'}</p>
										<p className="text-sm text-slate-500 mt-0.5">
											{requesterName} → {targetName}
										</p>
									</div>
									<span className={`text-[10px] font-semibold border px-2 py-1 rounded-full uppercase tracking-wider ${STATUS_STYLES[request.status]}`}>
										{request.status}
									</span>
								</div>

								{shiftChanged && (
									<div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
										Attenzione: il turno è stato modificato dopo la creazione di questa richiesta.
									</div>
								)}

								<div className="rounded-xl bg-slate-50 px-3 py-3 text-sm text-slate-600 space-y-1">
									<p><span className="text-slate-500">Richiedente:</span> {requesterName}</p>
									<p><span className="text-slate-500">Collega:</span> {targetName}</p>
									<p><span className="text-slate-500">Creata il:</span> {new Date(request.createdAt).toLocaleString('it-IT')}</p>
									{request.respondedAt && <p><span className="text-slate-500">Risposta il:</span> {new Date(request.respondedAt).toLocaleString('it-IT')}</p>}
								</div>

								{canRespond && (
									<div className="flex gap-2">
										<button
											onClick={() => handleDecision(request.id, 'rejected')}
											disabled={busyId === request.id}
											className="flex-1 flex items-center justify-center gap-2 rounded-xl border border-red-200 bg-red-50 py-3 text-sm font-semibold text-red-600 hover:bg-red-100 disabled:opacity-70"
										>
											{busyId === request.id ? <><span className="inline-block w-4 h-4 rounded-full border-2 border-red-400 border-t-transparent animate-spin" /> Aggiornamento...</> : 'Rifiuta'}
										</button>
										<button
											onClick={() => handleDecision(request.id, 'accepted')}
											disabled={busyId === request.id}
											className="flex-1 flex items-center justify-center gap-2 rounded-xl border border-green-200 bg-green-50 py-3 text-sm font-semibold text-green-700 hover:bg-green-100 disabled:opacity-70"
										>
											{busyId === request.id ? <><span className="inline-block w-4 h-4 rounded-full border-2 border-green-500 border-t-transparent animate-spin" /> Aggiornamento...</> : 'Accetta'}
										</button>
									</div>
								)}

								{canCancel && (
									<button
										onClick={() => handleCancel(request.id)}
										disabled={busyId === request.id}
										className="w-full flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white py-3 text-sm font-semibold text-slate-600 hover:bg-slate-100 disabled:opacity-70"
									>
										{busyId === request.id ? <><span className="inline-block w-4 h-4 rounded-full border-2 border-slate-400 border-t-transparent animate-spin" /> Annullamento...</> : 'Annulla richiesta'}
									</button>
								)}

								{!canRespond && !canCancel && request.status === 'pending' && (
									<p className="text-xs text-slate-500 italic">
										{isIncoming ? 'In attesa della tua risposta' : 'In attesa della risposta del collega'}
									</p>
								)}
							</div>
						);
					})}
				</div>
			)}
		</div>
	);
}
