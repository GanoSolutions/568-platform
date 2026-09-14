import { useCallback, useEffect, useState } from 'react';
import { employeeApi, userApi, UserStatus, type UserDTO } from '@/lib/apiClient';
import type { EmployeeDetail } from '@/types';

/**
 * Mappa un UserDTO del backend C# nel modello EmployeeDetail del frontend.
 *
 * I dati anagrafici (nome, cognome, codice fiscale, telefono, colore, fine
 * contratto) sono annidati in `dto.employee`, presente sempre dal momento
 * della creazione (Employee nasce insieme a User, PR #68) — resta null solo
 * per i vecchi account privilegiati (Admin/Manager) creati da seed, che non
 * hanno anagrafica.
 */
function mapUserDTOToEmployee(dto: UserDTO): EmployeeDetail {
	const emp = dto.employee;

	return {
		id: dto.id,
		name: emp ? `${emp.name} ${emp.surname}`.trim() : dto.email,
		// Cognome tenuto separato dal nome completo sopra (che resta per la sola
		// visualizzazione) solo per poter precompilare nome/cognome come due campi
		// distinti in EmployeeSheet — il backend li vuole separati (EmployeeCreate).
		surname: emp?.surname ?? '',
		email: dto.email,
		role: dto.role === 0 ? 'admin' : dto.role === 1 ? 'manager' : 'employee',
		color: emp?.color ?? '#6366f1',
		fiscalCode: emp?.fiscalCode ?? '',
		phone: emp?.phone ?? '',
		contractEnd: emp?.contractEnd ?? '',
		// Il backend non ha uno stato "Invited" separato (PR #68): un invito si può
		// (ri)generare solo da Pending, quindi "invited" qui significa "non più
		// Pending" — a conti fatti implica sempre anche firstLoginCompleted. Non è
		// una svista: è la stessa condizione che il backend richiede per accettare
		// un invito, quindi usarla per nascondere i pulsanti invito/link è corretta.
		invited: dto.status !== UserStatus.Pending,
		firstLoginCompleted: dto.status >= UserStatus.Active,
		disabled: dto.status === UserStatus.Disabled,
	};
}

interface EmployeeFormData {
	name: string;
	surname: string;
	fiscalCode: string;
	email: string;
	phone: string;
	contractEnd: string;
}

/**
 * Lista dipendenti condivisa a livello di modulo: Calendar, Requests ed
 * Employees montano tutti useEmployees() e, cambiando pagina, lo smontano e
 * rimontano — senza cache rifarebbero la stessa GET /user identica ad ogni
 * navigazione (segnalato da Hermann in review su PR #49). Refresh pagina
 * resetta il modulo e quindi la cache, volutamente: nessuna persistenza.
 */
let employeesCache: EmployeeDetail[] | null = null;

export function useEmployees() {
	const [employees, setEmployees] = useState<EmployeeDetail[]>(employeesCache ?? []);
	const [loading, setLoading] = useState(employeesCache === null);
	const [error, setError] = useState('');

	// 1. Pure async fetch function (NO synchronous setState at the start).
	// force=true (dopo una mutazione) salta la cache e la rimpiazza col dato fresco.
	const fetchEmployees = useCallback(async (force = false) => {
		if (!force && employeesCache) {
			setEmployees(employeesCache);
			setLoading(false);
			return;
		}
		try {
			const dtos = await userApi.getAll();
			const mapped = [...dtos]
				.sort((a, b) => a.createdAt.localeCompare(b.createdAt))
				.map(mapUserDTOToEmployee);
			employeesCache = mapped;
			setEmployees(mapped);
		} catch (loadError) {
			setError(loadError instanceof Error ? loadError.message : 'Errore nel caricamento dei dipendenti');
		} finally {
			setLoading(false);
		}
	}, []);

	// 2. Effect only handles initial mount. It jumps straight to the `await`.
	useEffect(() => {
		fetchEmployees();
	}, [fetchEmployees]);

	// 3. Reload function for AFTER mutations. Safe to have synchronous setState here.
	const reloadEmployees = useCallback(async () => {
		setLoading(true);
		setError('');
		await fetchEmployees(true);
	}, [fetchEmployees]);

	const addEmployee = async (data: EmployeeFormData) => {
		setError('');
		const created = await employeeApi.create({ ...data, contractEnd: data.contractEnd || null });
		await reloadEmployees();
		return created;
	};

	const updateEmployee = async (id: string, data: EmployeeFormData) => {
		setError('');
		await employeeApi.update(id, { ...data, contractEnd: data.contractEnd || null });
		await reloadEmployees();
	};

	const deleteEmployee = async (id: string) => {
		setError('');
		await userApi.delete(id);
		await reloadEmployees();
	};

	const sendInvite = async (id: string) => {
		setError('');
		await userApi.invite(id);
		await reloadEmployees();
	};

	const copyInviteLink = async (id: string): Promise<string> => {
		setError('');
		const { inviteToken } = await userApi.inviteLink(id);
		await reloadEmployees();
		return `${window.location.origin}/set-password?token=${inviteToken}`;
	};

	return { employees, loading, error, addEmployee, updateEmployee, deleteEmployee, sendInvite, copyInviteLink, reload: reloadEmployees };
}
