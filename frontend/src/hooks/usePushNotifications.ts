// usePushNotifications.ts
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '@/context/AuthContext';
import { ApiError, pushApi } from '@/lib/apiClient';

const pushServiceWorkerPath = '/push-sw-v2.js';

function urlBase64ToUint8Array(base64String: string): Uint8Array<ArrayBuffer> {
	const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
	const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
	const rawData = window.atob(base64);
	const outputArray = new Uint8Array(rawData.length);

	for (let index = 0; index < rawData.length; index += 1) {
		outputArray[index] = rawData.charCodeAt(index);
	}

	return outputArray;
}

/** Invia al backend C# la subscription del browser corrente (già creata/esistente). */
async function registerOnBackend(subscription: PushSubscription): Promise<void> {
	const subscriptionJson = subscription.toJSON();
	await pushApi.subscribe({
		endpoint: subscription.endpoint,
		p256dh: subscriptionJson.keys?.p256dh ?? '',
		auth: subscriptionJson.keys?.auth ?? '',
		userAgent: navigator.userAgent,
	});
}

// Cache a livello di modulo (stesso pattern di employeesCache in useEmployees):
// Requests è oggi l'unica pagina che monta questo hook, ma lasciarla e
// tornarci smonta/rimonta l'hook da zero — senza cache rifà il fetch della
// chiave VAPID e dello stato "enabled" ogni volta, con un flash visivo e
// chiamate sprecate (segnalato da Hermann in review su PR #74). La chiave
// VAPID è config statica del server, non cambia in sessione. Lo stato
// "enabled" invece è per-utente: invalidato esplicitamente al logout, così
// un secondo utente sullo stesso tab non eredita lo stato del primo.
let vapidPublicKeyCache: string | null = null;
let vapidCheckedCache = false;
let enabledCache = false;
let enabledCheckedCache = false;

export function usePushNotifications() {
	const { user } = useAuth();
	const [permission, setPermission] = useState(typeof Notification === 'undefined' ? 'unsupported' : Notification.permission);
	const [enabled, setEnabled] = useState(enabledCache);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState('');
	const [vapidPublicKey, setVapidPublicKey] = useState<string | null>(vapidPublicKeyCache);
	const [vapidChecked, setVapidChecked] = useState(vapidCheckedCache);

	useEffect(() => {
		if (typeof Notification === 'undefined') {
			setPermission('unsupported');
			return;
		}

		setPermission(Notification.permission);
	}, []);

	useEffect(() => {
		if (!user) {
			// Logout: la cache del prossimo utente sullo stesso tab riparte da zero.
			vapidPublicKeyCache = null;
			vapidCheckedCache = false;
			enabledCache = false;
			enabledCheckedCache = false;
			setVapidPublicKey(null);
			setVapidChecked(false);
			setEnabled(false);
			return;
		}

		if (vapidCheckedCache) {
			setVapidPublicKey(vapidPublicKeyCache);
			setVapidChecked(true);
			return;
		}

		let mounted = true;

		pushApi.vapidPublicKey()
			.then(({ publicKey }) => {
				vapidPublicKeyCache = publicKey;
				if (mounted) setVapidPublicKey(publicKey);
			})
			.catch((e) => {
				vapidPublicKeyCache = null;
				if (!mounted) return;
				setVapidPublicKey(null);
				// 404 = WebPush disabilitato/non configurato sul server, non un errore da mostrare.
				if (!(e instanceof ApiError && e.status === 404)) {
					setError((e as Error).message || 'Verifica configurazione notifiche non riuscita');
				}
			})
			.finally(() => {
				vapidCheckedCache = true;
				if (mounted) setVapidChecked(true);
			});

		return () => { mounted = false; };
	}, [user]);

	const availability = useMemo(() => {
		if (!user) return 'signed-out';
		if (!vapidChecked) return 'checking';
		if (!vapidPublicKey) return 'vapid-missing';
		if (!('serviceWorker' in navigator) || !('PushManager' in window)) return 'unsupported';
		return 'ready';
	}, [user, vapidChecked, vapidPublicKey]);

	// GET /push/status ritorna la subscription registrata per l'utente (modello
	// "una per utente": attivarne un'altra altrove sostituisce questa). Va
	// confrontato l'endpoint con quello del browser corrente, non basta che
	// entrambi esistano — altrimenti un browser con una subscription locale
	// ormai rimpiazzata da un altro dispositivo risulterebbe "attivo" lo stesso.
	useEffect(() => {
		if (availability !== 'ready') {
			setEnabled(false);
			return;
		}

		if (enabledCheckedCache) {
			setEnabled(enabledCache);
			return;
		}

		let mounted = true;

		const loadSubscriptionState = async () => {
			try {
				const [status, registration] = await Promise.all([
					pushApi.status(),
					navigator.serviceWorker.getRegistration(pushServiceWorkerPath),
				]);
				const subscription = await registration?.pushManager.getSubscription();
				const isEnabled = status.active && subscription?.endpoint === status.endpoint;
				enabledCache = isEnabled;
				enabledCheckedCache = true;
				if (mounted) setEnabled(isEnabled);
			} catch (loadError) {
				if (!mounted) return;
				setError((loadError as Error).message || 'Verifica stato notifiche non riuscita');
			}
		};

		loadSubscriptionState();

		return () => {
			mounted = false;
		};
	}, [availability]);

	const enable = useCallback(async () => {
		if (availability !== 'ready' || !vapidPublicKey) return;

		setLoading(true);
		setError('');

		try {
			const registrations = await navigator.serviceWorker.getRegistrations();
			await Promise.all(
				registrations
					.filter((registration) => !registration.active?.scriptURL.endsWith(pushServiceWorkerPath))
					.map((registration) => registration.unregister()),
			);

			const registration = await navigator.serviceWorker.register(pushServiceWorkerPath, {
				updateViaCache: 'none',
			});
			await registration.update();
			const requestedPermission = await Notification.requestPermission();
			setPermission(requestedPermission);

			if (requestedPermission !== 'granted') {
				throw new Error('Permesso notifiche non concesso');
			}

			let subscription = await registration.pushManager.getSubscription();

			if (!subscription) {
				subscription = await registration.pushManager.subscribe({
					userVisibleOnly: true,
					applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
				});
			}

			await registerOnBackend(subscription);
			enabledCache = true;
			enabledCheckedCache = true;
			setEnabled(true);
		} catch (enableError) {
			setError((enableError as Error).message || 'Attivazione notifiche non riuscita');
			throw enableError;
		} finally {
			setLoading(false);
		}
	}, [availability, vapidPublicKey]);

	const registerIfGranted = useCallback(async () => {
		if (availability !== 'ready' || !vapidPublicKey) return;
		if (Notification.permission !== 'granted') return;

		setLoading(true);
		setError('');

		try {
			const registration = await navigator.serviceWorker.register(pushServiceWorkerPath, {
				updateViaCache: 'none',
			});
			await registration.update();

			let subscription = await registration.pushManager.getSubscription();

			if (!subscription) {
				subscription = await registration.pushManager.subscribe({
					userVisibleOnly: true,
					applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
				});
			}

			await registerOnBackend(subscription);
			enabledCache = true;
			enabledCheckedCache = true;
			setEnabled(true);
		} catch (e) {
			setError((e as Error)?.message || 'Registrazione subscription non riuscita');
		} finally {
			setLoading(false);
		}
	}, [availability, vapidPublicKey]);

	// Disattivazione esplicita dalla UI (bottone "Disattiva notifiche"): a
	// differenza del logout, qui si disiscrive anche il browser (non solo il
	// backend) — l'utente ha chiesto di fermare le notifiche, non sta per
	// riloggarsi a breve.
	const disable = useCallback(async () => {
		setLoading(true);
		setError('');

		try {
			if ('serviceWorker' in navigator) {
				const registration = await navigator.serviceWorker.getRegistration(pushServiceWorkerPath);
				const subscription = await registration?.pushManager.getSubscription();

				if (subscription) {
					await pushApi.unsubscribe(subscription.endpoint);
					await subscription.unsubscribe();
				}
			}

			enabledCache = false;
			enabledCheckedCache = true;
			setEnabled(false);
		} catch (disableError) {
			setError((disableError as Error).message || 'Disattivazione notifiche non riuscita');
			throw disableError;
		} finally {
			setLoading(false);
		}
	}, []);

	// La unsubscribe al logout vive in AuthContext.logout(), non qui: questo
	// hook è montato solo nella pagina Richieste, e un logout da un'altra
	// pagina non lo sveglierebbe. In più il vecchio listener su app:logout
	// scattava anche sui logout forzati (token scaduto, sessione sostituita
	// da un login altrove), dove la subscription non va toccata nel nuovo
	// modello (sopravvive ai cambi di sessione). Resta solo app:login, per
	// ri-registrare la subscription sul backend quando il permesso è già
	// concesso (richiesto da Hermann in review su PR #74).
	useEffect(() => {
		const onAppLogin = () => {
			registerIfGranted().catch(() => { /* ignore */ });
		};

		window.addEventListener('app:login', onAppLogin);

		return () => {
			window.removeEventListener('app:login', onAppLogin);
		};
	}, [registerIfGranted]);

	return {
		availability,
		permission,
		enabled,
		loading,
		error,
		enable,
		disable,
	};
}
