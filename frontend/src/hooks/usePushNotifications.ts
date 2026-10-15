// usePushNotifications.ts
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '@/context/AuthContext';
import { pushApi } from '@/lib/apiClient';

const vapidPublicKey = import.meta.env.VITE_VAPID_PUBLIC_KEY;
const pushServiceWorkerPath = '/push-sw-v2.js';

function isPlaceholder(value: string | undefined, placeholder: string): boolean {
	return !value || value === placeholder;
}

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

export const isWebPushConfigured = !isPlaceholder(vapidPublicKey, 'your-vapid-public-key-here');

export function usePushNotifications() {
	const { user } = useAuth();
	const [permission, setPermission] = useState(typeof Notification === 'undefined' ? 'unsupported' : Notification.permission);
	const [enabled, setEnabled] = useState(false);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState('');

	useEffect(() => {
		if (typeof Notification === 'undefined') {
			setPermission('unsupported');
			return;
		}

		setPermission(Notification.permission);
	}, []);

	const availability = useMemo(() => {
		if (!user) return 'signed-out';
		if (!isWebPushConfigured) return 'vapid-missing';
		if (!('serviceWorker' in navigator) || !('PushManager' in window)) return 'unsupported';
		return 'ready';
	}, [user]);

	// GET /push/status dice se l'utente ha almeno una subscription attiva su
	// QUALSIASI dispositivo — va incrociato con la subscription di questo browser,
	// altrimenti un utente iscritto dal telefono vedrebbe "attivo" anche sul PC
	// dove non lo è mai stato.
	useEffect(() => {
		if (availability !== 'ready') {
			setEnabled(false);
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
				if (!mounted) return;
				setEnabled(status.active && !!subscription);
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
		if (availability !== 'ready') return;

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
			setEnabled(true);
		} catch (enableError) {
			setError((enableError as Error).message || 'Attivazione notifiche non riuscita');
			throw enableError;
		} finally {
			setLoading(false);
		}
	}, [availability]);

	const registerIfGranted = useCallback(async () => {
		if (availability !== 'ready') return;
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
			setEnabled(true);
		} catch (e) {
			setError((e as Error)?.message || 'Registrazione subscription non riuscita');
		} finally {
			setLoading(false);
		}
	}, [availability]);

	const disable = useCallback(async () => {
		setLoading(true);
		setError('');

		try {
			if ('serviceWorker' in navigator) {
				const registration = await navigator.serviceWorker.getRegistration(pushServiceWorkerPath);
				const subscription = await registration?.pushManager.getSubscription();

				if (subscription) {
					// Il logout forzato (refresh token scaduto) arriva qui con i token già
					// azzerati: la unsubscribe lato backend va in 401. Disiscriviamo comunque
					// il browser — il backend disattiva da solo le subscription scadute al
					// prossimo invio (404/410 dal push service).
					await pushApi.unsubscribe(subscription.endpoint).catch(() => { /* vedi sopra */ });
					await subscription.unsubscribe();
				}
			}

			setEnabled(false);
		} catch (disableError) {
			setError((disableError as Error).message || 'Disattivazione notifiche non riuscita');
			throw disableError;
		} finally {
			setLoading(false);
		}
	}, []);

	useEffect(() => {
		// app:logout porta nel detail un array `pending`: ci iscriviamo con la
		// promise di disable() così AuthContext.logout() può aspettarla (con
		// timeout breve) prima di cancellare i token — altrimenti la unsubscribe,
		// che richiede ancora autenticazione, rischia di partire dopo che i token
		// sono già stati azzerati (richiesto da Hermann, guida frontend PR #71).
		const onAppLogout = (event: Event) => {
			const detail = (event as CustomEvent<{ pending?: Promise<unknown>[] }>).detail;
			detail?.pending?.push(disable().catch(() => { /* ignore */ }));
		};

		const onAppLogin = () => {
			registerIfGranted().catch(() => { /* ignore */ });
		};

		window.addEventListener('app:logout', onAppLogout);
		window.addEventListener('app:login', onAppLogin);

		return () => {
			window.removeEventListener('app:logout', onAppLogout);
			window.removeEventListener('app:login', onAppLogin);
		};
	}, [disable, registerIfGranted]);

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
