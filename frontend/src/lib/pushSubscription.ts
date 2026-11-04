// Logica push legata alla sessione, non a una pagina: viene chiamata da
// AuthContext, che è sempre montato, mentre usePushNotifications vive solo in
// Richieste. Sta qui anche per evitare un ciclo (quell'hook usa useAuth).
import { pushApi } from '@/lib/apiClient';

export const PUSH_SERVICE_WORKER_PATH = '/push-sw-v2.js';

const LOGOUT_UNSUBSCRIBE_TIMEOUT_MS = 1500;

// Utente che ha attivato la subscription di questo browser. Non è legato ai
// token: deve sopravvivere al logout per riconoscere chi rientra.
const SUBSCRIPTION_OWNER_KEY = 'push_subscription_owner';

// Cache a livello di modulo (stesso pattern di employeesCache in useEmployees):
// lasciare Richieste e tornarci smonta/rimonta l'hook da zero — senza cache
// rifà il fetch della chiave VAPID e dello stato "enabled" ogni volta, con un
// flash visivo e chiamate sprecate (segnalato da Hermann in review su PR #74).
export const pushCache = {
	vapidPublicKey: null as string | null,
	vapidChecked: false,
	enabled: false,
	enabledChecked: false,
};

/** Da chiamare a ogni fine sessione: lo stato "enabled" è per-utente. */
export function resetPushCache(): void {
	pushCache.vapidPublicKey = null;
	pushCache.vapidChecked = false;
	pushCache.enabled = false;
	pushCache.enabledChecked = false;
}

/** Invia al backend C# la subscription del browser corrente (già creata/esistente). */
export async function registerOnBackend(subscription: PushSubscription, userId: string): Promise<void> {
	const subscriptionJson = subscription.toJSON();
	await pushApi.subscribe({
		endpoint: subscription.endpoint,
		p256dh: subscriptionJson.keys?.p256dh ?? '',
		auth: subscriptionJson.keys?.auth ?? '',
		userAgent: navigator.userAgent,
	});
	localStorage.setItem(SUBSCRIPTION_OWNER_KEY, userId);
}

async function getBrowserSubscription(): Promise<PushSubscription | null> {
	if (!('serviceWorker' in navigator)) return null;
	const registration = await navigator.serviceWorker.getRegistration(PUSH_SERVICE_WORKER_PATH);
	return (await registration?.pushManager.getSubscription()) ?? null;
}

let pendingLoginSync: Promise<void> = Promise.resolve();

/** Permette all'hook di leggere lo stato solo dopo la fine della sync di login. */
export function waitForPushLoginSync(): Promise<void> {
	return pendingLoginSync;
}

/**
 * Dopo un login ri-registra sul backend la subscription rimasta nel browser
 * (il logout cancella la riga ma lascia la subscription locale, così non serve
 * chiedere di nuovo il permesso).
 *
 * Non ne crea di nuove: "Disattiva notifiche" rimuove la subscription locale,
 * quindi crearne una qui annullerebbe la scelta dell'utente. Non registra
 * nemmeno se l'utente ha già una subscription attiva: con il modello "una per
 * utente" un login (o un reload) qui toglierebbe le notifiche all'altro
 * dispositivo senza che nessuno lo abbia chiesto.
 *
 * Se la subscription è di un altro utente la rimuove dal browser: il nuovo
 * utente non la eredita e, dopo un logout forzato, smettono di arrivare qui le
 * notifiche del precedente. La sua riga la cancella il backend al primo invio,
 * quando il push service risponde 404/410.
 */
export function syncPushSubscriptionOnLogin(userId: string): void {
	pendingLoginSync = (async () => {
		try {
			if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return;

			const subscription = await getBrowserSubscription();
			if (!subscription) return;

			const status = await pushApi.status();
			const isOwner = localStorage.getItem(SUBSCRIPTION_OWNER_KEY) === userId || status.endpoint === subscription.endpoint;
			if (!isOwner) {
				await subscription.unsubscribe();
				return;
			}

			localStorage.setItem(SUBSCRIPTION_OWNER_KEY, userId);
			if (!status.active) await registerOnBackend(subscription, userId);
		} catch {
			// best-effort: l'utente può sempre riattivarle da Richieste
		}
	})();
}

/**
 * Cancella la riga sul backend PRIMA che il logout cancelli i token
 * (/push/unsubscribe richiede autenticazione). Non tocca la subscription nel
 * browser, così il prossimo login la ri-registra. Best-effort e con timeout:
 * una rete lenta non deve bloccare il logout.
 */
export async function unsubscribeBackendOnLogout(): Promise<void> {
	const unsubscribe = async () => {
		await pendingLoginSync;
		const subscription = await getBrowserSubscription();
		if (subscription) await pushApi.unsubscribe(subscription.endpoint);
	};

	let timer: ReturnType<typeof setTimeout> | undefined;
	const timeout = new Promise<void>((resolve) => {
		timer = setTimeout(resolve, LOGOUT_UNSUBSCRIBE_TIMEOUT_MS);
	});

	try {
		await Promise.race([unsubscribe(), timeout]);
	} catch {
		// best-effort: non blocchiamo il logout per un errore sulla unsubscribe
	} finally {
		clearTimeout(timer);
	}
}
