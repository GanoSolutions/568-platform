// usePushNotifications.ts
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '@/context/AuthContext';
import { ApiError, pushApi } from '@/lib/apiClient';
import { PUSH_SERVICE_WORKER_PATH, pushCache, registerOnBackend, waitForPushLoginSync } from '@/lib/pushSubscription';

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

export function usePushNotifications() {
	const { user } = useAuth();
	const [permission, setPermission] = useState<NotificationPermission | 'unsupported'>(typeof Notification === 'undefined' ? 'unsupported' : Notification.permission);
	const [enabled, setEnabled] = useState(pushCache.enabled);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState('');
	const [vapidPublicKey, setVapidPublicKey] = useState<string | null>(pushCache.vapidPublicKey);
	const [vapidChecked, setVapidChecked] = useState(pushCache.vapidChecked);

	useEffect(() => {
		if (!user) {
			setVapidPublicKey(null);
			setVapidChecked(false);
			setEnabled(false);
			return;
		}

		if (pushCache.vapidChecked) {
			setVapidPublicKey(pushCache.vapidPublicKey);
			setVapidChecked(true);
			return;
		}

		let mounted = true;

		pushApi.vapidPublicKey()
			.then(({ publicKey }) => {
				pushCache.vapidPublicKey = publicKey;
				if (mounted) setVapidPublicKey(publicKey);
			})
			.catch((e) => {
				pushCache.vapidPublicKey = null;
				if (!mounted) return;
				setVapidPublicKey(null);
				// 404 = WebPush disabilitato/non configurato sul server, non un errore da mostrare.
				if (!(e instanceof ApiError && e.status === 404)) {
					setError((e as Error).message || 'Verifica configurazione notifiche non riuscita');
				}
			})
			.finally(() => {
				pushCache.vapidChecked = true;
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

		if (pushCache.enabledChecked) {
			setEnabled(pushCache.enabled);
			return;
		}

		let mounted = true;

		const loadSubscriptionState = async () => {
			try {
				await waitForPushLoginSync();
				const [status, registration] = await Promise.all([
					pushApi.status(),
					navigator.serviceWorker.getRegistration(PUSH_SERVICE_WORKER_PATH),
				]);
				const subscription = await registration?.pushManager.getSubscription();
				if (!mounted) return;
				const isEnabled = status.active && subscription?.endpoint === status.endpoint;
				pushCache.enabled = isEnabled;
				pushCache.enabledChecked = true;
				setEnabled(isEnabled);
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
		if (!user || availability !== 'ready' || !vapidPublicKey) return;

		setLoading(true);
		setError('');

		try {
			const registrations = await navigator.serviceWorker.getRegistrations();
			await Promise.all(
				registrations
					.filter((registration) => !registration.active?.scriptURL.endsWith(PUSH_SERVICE_WORKER_PATH))
					.map((registration) => registration.unregister()),
			);

			const registration = await navigator.serviceWorker.register(PUSH_SERVICE_WORKER_PATH, {
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

			await registerOnBackend(subscription, user.id);
			pushCache.enabled = true;
			pushCache.enabledChecked = true;
			setEnabled(true);
		} catch (enableError) {
			setError((enableError as Error).message || 'Attivazione notifiche non riuscita');
			throw enableError;
		} finally {
			setLoading(false);
		}
	}, [user, availability, vapidPublicKey]);

	// Disattivazione esplicita dalla UI (bottone "Disattiva notifiche"): a
	// differenza del logout, qui si disiscrive anche il browser (non solo il
	// backend) — l'utente ha chiesto di fermare le notifiche, non sta per
	// riloggarsi a breve.
	const disable = useCallback(async () => {
		setLoading(true);
		setError('');

		try {
			if ('serviceWorker' in navigator) {
				const registration = await navigator.serviceWorker.getRegistration(PUSH_SERVICE_WORKER_PATH);
				const subscription = await registration?.pushManager.getSubscription();

				if (subscription) {
					await pushApi.unsubscribe(subscription.endpoint);
					await subscription.unsubscribe();
				}
			}

			pushCache.enabled = false;
			pushCache.enabledChecked = true;
			setEnabled(false);
		} catch (disableError) {
			setError((disableError as Error).message || 'Disattivazione notifiche non riuscita');
			throw disableError;
		} finally {
			setLoading(false);
		}
	}, []);

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
