import { describe, it, expect } from 'vitest';
import { mapUserDTO, getUserIdFromToken } from '../AuthContext';
import type { UserDTO } from '@/lib/apiClient';

// ─── A2: mapUserDTO ───────────────────────────────────────────────────────────

const baseDTO: UserDTO = {
	id: 'a1b2c3d4-0000-0000-0000-000000000001',
	email: 'mario@example.com',
	fullName: 'Mario Rossi',
	role: 2,      // Employee
	status: 1,    // Active
	color: '#6366f1',
	createdAt: '2026-01-01T00:00:00Z',
};

describe('mapUserDTO', () => {
	it('mappa correttamente i campi base', () => {
		const user = mapUserDTO(baseDTO);
		expect(user.id).toBe(baseDTO.id);
		expect(user.name).toBe('Mario Rossi');
		expect(user.email).toBe('mario@example.com');
		expect(user.color).toBe('#6366f1');
	});

	it('role Employee(2) → "employee"', () => {
		expect(mapUserDTO({ ...baseDTO, role: 2 }).role).toBe('employee');
	});

	it('role Admin(0) → "admin"', () => {
		expect(mapUserDTO({ ...baseDTO, role: 0 }).role).toBe('admin');
	});

	it('role Manager(1) → "admin"', () => {
		expect(mapUserDTO({ ...baseDTO, role: 1 }).role).toBe('admin');
	});

	it('status Active(1) → firstLoginCompleted true', () => {
		expect(mapUserDTO({ ...baseDTO, status: 1 }).firstLoginCompleted).toBe(true);
	});

	it('status Pending(0) → firstLoginCompleted false', () => {
		expect(mapUserDTO({ ...baseDTO, status: 0 }).firstLoginCompleted).toBe(false);
	});

	it('status Disabled(2) → firstLoginCompleted true', () => {
		expect(mapUserDTO({ ...baseDTO, status: 2 }).firstLoginCompleted).toBe(true);
	});

	it('color null → colore di default #6366f1', () => {
		expect(mapUserDTO({ ...baseDTO, color: null }).color).toBe('#6366f1');
	});
});

// ─── A3: getUserIdFromToken ───────────────────────────────────────────────────

describe('getUserIdFromToken', () => {
	function makeJwt(payload: Record<string, unknown>): string {
		const encoded = btoa(JSON.stringify(payload)).replace(/=/g, '');
		return `header.${encoded}.signature`;
	}

	it('estrae il "sub" claim dal JWT', () => {
		const token = makeJwt({ sub: 'user-guid-here', exp: 9999999999 });
		expect(getUserIdFromToken(token)).toBe('user-guid-here');
	});

	it('restituisce null se il token è malformato', () => {
		expect(getUserIdFromToken('not.a.jwt')).toBeNull();
	});

	it('restituisce null se sub non è una stringa', () => {
		const token = makeJwt({ sub: 42 });
		expect(getUserIdFromToken(token)).toBeNull();
	});
});
