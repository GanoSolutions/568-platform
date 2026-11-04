import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { useEffect, type ReactNode } from 'react';

const push = vi.hoisted(() => ({
	vapidPublicKey: vi.fn(),
	status: vi.fn(),
	subscribe: vi.fn(),
	unsubscribe: vi.fn(),
}));
const auth = vi.hoisted(() => ({ login: vi.fn(), logout: vi.fn() }));
const users = vi.hoisted(() => ({ getById: vi.fn() }));

vi.mock('@/lib/apiClient', async (importOriginal) => ({
	...(await importOriginal<typeof import('@/lib/apiClient')>()),
	pushApi: push,
	authApi: auth,
	userApi: users,
}));

import { AuthProvider, useAuth } from '@/context/AuthContext';
import { usePushNotifications } from '@/hooks/usePushNotifications';
import { clearTokens } from '@/lib/apiClient';
import { pushCache, resetPushCache, unsubscribeBackendOnLogout } from '@/lib/pushSubscription';

const ENDPOINT = 'https://fcm.googleapis.com/fcm/send/this-browser';
// JWT con solo il claim nameid: il login ne legge l'id utente
const TOKEN = `x.${btoa(JSON.stringify({ nameid: 'user-1' }))}.y`;

const OWNER_KEY = 'push_subscription_owner';

type FakeSubscription = { endpoint: string; toJSON: () => unknown; unsubscribe: () => Promise<boolean> };
let browserSubscription: FakeSubscription | null;

function makeSubscription(endpoint = ENDPOINT): FakeSubscription {
	return {
		endpoint,
		toJSON: () => ({ keys: { p256dh: 'p', auth: 'a' } }),
		unsubscribe: async () => { browserSubscription = null; return true; },
	};
}

function userDto(id: string) {
	return { id, email: `${id}@b.it`, role: 2, status: 2, employee: null, createdAt: '' };
}

function installBrowser() {
	const registration = {
		active: { scriptURL: 'http://localhost/push-sw-v2.js' },
		unregister: () => Promise.resolve(true),
		pushManager: {
			getSubscription: () => Promise.resolve(browserSubscription),
			subscribe: async () => { browserSubscription = makeSubscription(`${ENDPOINT}-new`); return browserSubscription; },
		},
		update: () => Promise.resolve(),
	};
	Object.defineProperty(navigator, 'serviceWorker', {
		configurable: true,
		value: {
			getRegistration: () => Promise.resolve(registration),
			getRegistrations: () => Promise.resolve([registration]),
			register: () => Promise.resolve(registration),
		},
	});
	vi.stubGlobal('PushManager', class {});
	vi.stubGlobal('Notification', { permission: 'granted', requestPermission: () => Promise.resolve('granted') });
}

function wrapper({ children }: { children: ReactNode }) {
	return <AuthProvider>{children}</AuthProvider>;
}

// La pagina Richieste è montata solo se `open`, come nell'app reale
function App({ open }: { open: boolean }) {
	const { user } = useAuth();
	return <div>{user ? 'logged-in' : 'logged-out'}{open && user && <PushState />}</div>;
}
function PushState() {
	const { enabled, availability, enable } = usePushNotifications();
	return <button data-testid="push" onClick={enable}>{availability}:{String(enabled)}</button>;
}

let authValue: ReturnType<typeof useAuth>;
function Capture() {
	const value = useAuth();
	useEffect(() => { authValue = value; });
	return null;
}

function renderApp(open = false) {
	return render(<App open={open} />, {
		wrapper: ({ children }) => wrapper({ children: <><Capture />{children}</> }),
	});
}

beforeEach(() => {
	vi.clearAllMocks();
	clearTokens();
	resetPushCache();
	installBrowser();
	localStorage.setItem(OWNER_KEY, 'user-1');
	browserSubscription = makeSubscription();
	push.vapidPublicKey.mockResolvedValue({ publicKey: 'AAAA' });
	push.subscribe.mockResolvedValue(undefined);
	push.unsubscribe.mockResolvedValue(undefined);
	auth.login.mockResolvedValue({ accessToken: TOKEN, refreshToken: 'r' });
	auth.logout.mockResolvedValue(undefined);
	users.getById.mockResolvedValue(userDto('user-1'));
});

describe('push al login', () => {
	it('ri-registra la subscription del browser se il backend non ne ha una', async () => {
		push.status.mockResolvedValue({ active: false, endpoint: null });
		renderApp();
		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });

		await waitFor(() => expect(push.subscribe).toHaveBeenCalledTimes(1));
		expect(push.subscribe.mock.calls[0][0]).toMatchObject({ endpoint: ENDPOINT, p256dh: 'p', auth: 'a' });
	});

	it('non toglie le notifiche a un altro dispositivo già attivo', async () => {
		push.status.mockResolvedValue({ active: true, endpoint: 'https://fcm.googleapis.com/fcm/send/phone' });
		renderApp();
		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });

		await waitFor(() => expect(push.status).toHaveBeenCalled());
		await act(async () => { await Promise.resolve(); });
		expect(push.subscribe).not.toHaveBeenCalled();
	});

	it('non riattiva le notifiche se l\'utente le aveva disattivate', async () => {
		browserSubscription = null;
		push.status.mockResolvedValue({ active: false, endpoint: null });
		renderApp();
		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });
		await act(async () => { await Promise.resolve(); });

		expect(push.subscribe).not.toHaveBeenCalled();
	});

	it('un altro utente non eredita la subscription: viene rimossa dal browser', async () => {
		push.status.mockResolvedValue({ active: false, endpoint: null });
		users.getById.mockResolvedValue(userDto('user-2'));
		const view = renderApp();
		await act(async () => { await authValue.loginWithPassword('b@b.it', 'x'); });

		await waitFor(() => expect(browserSubscription).toBeNull());
		expect(push.subscribe).not.toHaveBeenCalled();

		view.rerender(<App open />);
		await waitFor(() => expect(screen.getByTestId('push').textContent).toBe('ready:false'));
	});

	it('la subscription già registrata per l\'utente è sua anche senza proprietario salvato', async () => {
		localStorage.removeItem(OWNER_KEY);
		push.status.mockResolvedValue({ active: true, endpoint: ENDPOINT });
		renderApp();
		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });

		await waitFor(() => expect(localStorage.getItem(OWNER_KEY)).toBe('user-1'));
		expect(browserSubscription).not.toBeNull();
	});

	it('dopo che il secondo utente attiva le notifiche, il suo login le ri-registra', async () => {
		let backendEndpoint: string | null = null;
		push.status.mockImplementation(async () => ({ active: backendEndpoint !== null, endpoint: backendEndpoint }));
		push.subscribe.mockImplementation(async (body: { endpoint: string }) => { backendEndpoint = body.endpoint; });
		push.unsubscribe.mockImplementation(async () => { backendEndpoint = null; });
		users.getById.mockResolvedValue(userDto('user-2'));

		const view = renderApp(true);
		await act(async () => { await authValue.loginWithPassword('b@b.it', 'x'); });
		await waitFor(() => expect(screen.getByTestId('push').textContent).toBe('ready:false'));

		await act(async () => { screen.getByTestId('push').click(); });
		await waitFor(() => expect(backendEndpoint).toBe(`${ENDPOINT}-new`));
		expect(localStorage.getItem(OWNER_KEY)).toBe('user-2');

		view.rerender(<App open={false} />);
		await act(async () => { await authValue.logout(); });
		expect(backendEndpoint).toBeNull();

		await act(async () => { await authValue.loginWithPassword('b@b.it', 'x'); });
		await waitFor(() => expect(backendEndpoint).toBe(`${ENDPOINT}-new`));
	});

	it('non fa nulla senza permesso notifiche', async () => {
		vi.stubGlobal('Notification', { permission: 'default' });
		renderApp();
		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });
		await act(async () => { await Promise.resolve(); });

		expect(push.status).not.toHaveBeenCalled();
		expect(push.subscribe).not.toHaveBeenCalled();
	});
});

describe('push al logout', () => {
	it('logout e nuovo login sullo stesso tab: stato aggiornato e notifiche ancora attive', async () => {
		// Sul backend la riga esiste finché non arriva unsubscribe, e torna con subscribe
		let backendEndpoint: string | null = null;
		push.status.mockImplementation(async () => ({ active: backendEndpoint !== null, endpoint: backendEndpoint }));
		push.subscribe.mockImplementation(async (body: { endpoint: string }) => { backendEndpoint = body.endpoint; });
		push.unsubscribe.mockImplementation(async () => { backendEndpoint = null; });

		const view = renderApp();
		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });
		await waitFor(() => expect(backendEndpoint).toBe(ENDPOINT));

		view.rerender(<App open />);
		await waitFor(() => expect(screen.getByTestId('push').textContent).toBe('ready:true'));
		view.rerender(<App open={false} />);

		await act(async () => { await authValue.logout(); });
		expect(push.unsubscribe).toHaveBeenCalledWith(ENDPOINT);
		expect(backendEndpoint).toBeNull();
		expect(pushCache.enabledChecked).toBe(false);

		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });
		await waitFor(() => expect(backendEndpoint).toBe(ENDPOINT));

		view.rerender(<App open />);
		await waitFor(() => expect(screen.getByTestId('push').textContent).toBe('ready:true'));
	});

	it('un altro utente sullo stesso tab non eredita lo stato del precedente', async () => {
		pushCache.enabled = true;
		pushCache.enabledChecked = true;
		push.status.mockResolvedValue({ active: true, endpoint: 'https://fcm.googleapis.com/fcm/send/phone' });

		const view = renderApp();
		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });
		await act(async () => { await authValue.logout(); });
		await act(async () => { await authValue.loginWithPassword('b@b.it', 'x'); });

		view.rerender(<App open />);
		await waitFor(() => expect(screen.getByTestId('push').textContent).toBe('ready:false'));
	});

	it('il logout forzato azzera la cache ma non cancella la subscription', async () => {
		push.status.mockResolvedValue({ active: true, endpoint: ENDPOINT });
		renderApp();
		await act(async () => { await authValue.loginWithPassword('a@b.it', 'x'); });
		pushCache.enabled = true;
		pushCache.enabledChecked = true;

		act(() => { window.dispatchEvent(new Event('app:logout')); });

		expect(pushCache.enabledChecked).toBe(false);
		expect(push.unsubscribe).not.toHaveBeenCalled();
	});

	it('una unsubscribe bloccata non blocca il logout', async () => {
		vi.useFakeTimers();
		try {
			push.unsubscribe.mockReturnValue(new Promise(() => {}));
			let done = false;
			unsubscribeBackendOnLogout().then(() => { done = true; });

			await vi.advanceTimersByTimeAsync(1499);
			expect(push.unsubscribe).toHaveBeenCalled();
			expect(done).toBe(false);

			await vi.advanceTimersByTimeAsync(1);
			expect(done).toBe(true);
		} finally {
			vi.useRealTimers();
		}
	});

	it('un errore della unsubscribe non blocca il logout', async () => {
		push.unsubscribe.mockRejectedValue(new Error('rete'));
		await expect(unsubscribeBackendOnLogout()).resolves.toBeUndefined();
	});
});
