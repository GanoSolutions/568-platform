import { beforeEach, describe, expect, it } from 'vitest';
import { getAppHubConnection, resetIntentionalStop, wasConnectionStoppedIntentionally } from '../realtime';

describe('realtime', () => {
	beforeEach(() => {
		getAppHubConnection();
		resetIntentionalStop();
	});

	it('considera voluta la chiusura della connessione quando la pagina si sta chiudendo', () => {
		expect(wasConnectionStoppedIntentionally()).toBe(false);

		window.dispatchEvent(new Event('beforeunload'));

		expect(wasConnectionStoppedIntentionally()).toBe(true);
	});

	it('un nuovo tentativo di connessione riporta il flag a false', () => {
		window.dispatchEvent(new Event('beforeunload'));

		resetIntentionalStop();

		expect(wasConnectionStoppedIntentionally()).toBe(false);
	});
});
