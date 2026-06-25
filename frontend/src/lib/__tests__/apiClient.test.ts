import { describe, it, expect } from 'vitest';
import { isTokenExpiringSoon } from '../apiClient';

/** Costruisce un JWT fittizio (header/payload/signature) con il payload dato. */
function makeJwt(payload: Record<string, unknown>): string {
	const encoded = btoa(JSON.stringify(payload)).replace(/=/g, '');
	return `header.${encoded}.signature`;
}

const nowSeconds = () => Math.floor(Date.now() / 1000);

describe('isTokenExpiringSoon', () => {
	it('false per un token con scadenza ampiamente futura', () => {
		expect(isTokenExpiringSoon(makeJwt({ exp: nowSeconds() + 3600 }))).toBe(false);
	});

	it('true per un token già scaduto', () => {
		expect(isTokenExpiringSoon(makeJwt({ exp: nowSeconds() - 10 }))).toBe(true);
	});

	it('true per un token che scade entro la finestra di 5 secondi', () => {
		expect(isTokenExpiringSoon(makeJwt({ exp: nowSeconds() + 3 }))).toBe(true);
	});

	it('false se manca il claim exp', () => {
		expect(isTokenExpiringSoon(makeJwt({ nameid: 'x' }))).toBe(false);
	});

	it('false per un token malformato', () => {
		expect(isTokenExpiringSoon('not.a.jwt')).toBe(false);
	});
});
