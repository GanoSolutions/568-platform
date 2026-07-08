import { useCallback, useEffect, useState } from 'react';
import { isSupabaseConfigured, supabase } from '@/lib/supabase';
import { userApi, UserStatus, type UserDTO } from '@/lib/apiClient';
import { getNextColor } from '@/lib/colorUtils';
import type { EmployeeDetail } from '@/types';

/**
 * Mappa un UserDTO del backend C# nel modello EmployeeDetail del frontend.
 *
 * I dati anagrafici (nome, cognome, codice fiscale, telefono, colore, fine
 * contratto) sono annidati in `dto.employee`, che è presente solo dopo che
 * l'utente ha accettato l'invito; prima di allora abbiamo solo email e stato.
 */
function mapUserDTOToEmployee(dto: UserDTO): EmployeeDetail {
	const emp = dto.employee;

	return {
		id: dto.id,
		name: emp ? `${emp.name} ${emp.surname}`.trim() : dto.email,
		email: dto.email,
		role: dto.role === 0 ? 'admin' : dto.role === 1 ? 'manager' : 'employee',
		color: emp?.color ?? '#6366f1',
		fiscalCode: emp?.fiscalCode ?? '',
		phone: emp?.phone ?? '',
		contractEnd: emp?.contractEnd ?? '',
		// invito esistente = qualsiasi stato oltre Pending; primo accesso completato = Active o Disabled
		invited: dto.status !== UserStatus.Pending,
		firstLoginCompleted: dto.status >= UserStatus.Active,
	};
}

interface EmployeeFormData {
	name: string;
	fiscalCode: string;
	email: string;
	phone: string;
	contractEnd: string;
}

export function useEmployees() {
	const [employees, setEmployees] = useState<EmployeeDetail[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState('');

	// 1. Pure async fetch function (NO synchronous setState at the start)
	const fetchEmployees = useCallback(async () => {
		try {
			const dtos = await userApi.getAll();
			const mapped = [...dtos]
				.sort((a, b) => a.createdAt.localeCompare(b.createdAt))
				.map(mapUserDTOToEmployee);
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
		await fetchEmployees();
	}, [fetchEmployees]);

	const addEmployee = async (data: EmployeeFormData) => {
		const color = getNextColor(employees);
		if (!isSupabaseConfigured || !supabase) {
			const newEmp: EmployeeDetail = {
				id: Date.now().toString(),
				...data,
				color,
				invited: false,
				firstLoginCompleted: false,
			};
			setEmployees(prev => [...prev, newEmp]);
			return newEmp;
		}

		setError('');

		const { data: profileRow, error: profileError } = await supabase
			.from('profiles')
			.insert({
				full_name: data.name,
				email: data.email,
				role: 'employee',
				color,
				first_login_completed: false,
			})
			.select('id')
			.single();

		if (profileError) {
			setError(profileError.message);
			throw profileError;
		}

		const { error: employeeError } = await supabase
			.from('employees')
			.insert({
				profile_id: profileRow.id,
				fiscal_code: data.fiscalCode,
				phone: data.phone,
				contract_end: data.contractEnd || null,
				invited: false,
			});

		if (employeeError) {
			setError(employeeError.message);
			throw employeeError;
		}

		await reloadEmployees();
		return profileRow;
	};

	const updateEmployee = async (id: string, data: EmployeeFormData) => {
		if (!isSupabaseConfigured || !supabase) {
			setEmployees(prev => prev.map(e => e.id === id ? { ...e, ...data } : e));
			return;
		}

		setError('');

		const { error: profileError } = await supabase
			.from('profiles')
			.update({
				full_name: data.name,
				email: data.email,
			})
			.eq('id', id);

		if (profileError) {
			setError(profileError.message);
			throw profileError;
		}

		const { error: employeeError } = await supabase
			.from('employees')
			.update({
				fiscal_code: data.fiscalCode,
				phone: data.phone,
				contract_end: data.contractEnd || null,
			})
			.eq('profile_id', id);

		if (employeeError) {
			setError(employeeError.message);
			throw employeeError;
		}

		await reloadEmployees();
	};

	const deleteEmployee = async (id: string) => {
		if (!isSupabaseConfigured || !supabase) {
			setEmployees(prev => prev.filter(e => e.id !== id));
			return;
		}

		setError('');
		const { error: deleteError } = await supabase
			.from('profiles')
			.delete()
			.eq('id', id);

		if (deleteError) {
			setError(deleteError.message);
			throw deleteError;
		}

		await reloadEmployees();
	};

	const sendInvite = async (id: string) => {
		if (!isSupabaseConfigured || !supabase) {
			setEmployees(prev => prev.map(e => e.id === id ? { ...e, invited: true } : e));
			return;
		}

		setError('');
		const { data: invokeData, error: inviteError } = await supabase.functions.invoke('send-invite', {
			body: { profileId: id },
		});

		// Estrai il messaggio reale dal corpo della risposta HTTP se disponibile
		let errMsg: string | null = null;
		if (inviteError) {
			try {
				// FunctionsHttpError ha un context che è la Response originale
				// eslint-disable-next-line @typescript-eslint/no-explicit-any
				const body = await (inviteError as any).context?.json?.();
				errMsg = body?.error ?? inviteError.message;
			} catch {
				errMsg = inviteError.message;
			}
		}

		console.log('[sendInvite] response:', { invokeData, inviteError, errMsg });

		if (inviteError) {
			const msg = errMsg ?? 'Errore sconosciuto';
			setError(`Errore invito: ${msg}`);
			throw new Error(msg);
		}

		// La funzione può rispondere con { error: '...' } anche con status 200
		if (invokeData?.error) {
			const msg = invokeData.error as string;
			setError(`Errore invito: ${msg}`);
			throw new Error(msg);
		}

		await reloadEmployees();
	};

	const copyInviteLink = async (id: string): Promise<string> => {
		if (!isSupabaseConfigured || !supabase) throw new Error('Supabase non configurato');

		const { data, error: invokeError } = await supabase.functions.invoke('send-invite', {
			body: { profileId: id, copyOnly: true },
		});

		let errMsg: string | null = null;
		if (invokeError) {
			try {
				const body = await (invokeError as any).context?.json?.();
				errMsg = body?.error ?? invokeError.message;
			} catch {
				errMsg = invokeError.message;
			}
			throw new Error(errMsg ?? 'Errore generazione link');
		}

		if (data?.error) throw new Error(data.error as string);
		if (!data?.link) throw new Error('Link non ricevuto');

		await reloadEmployees();
		return data.link as string;
	};

	return { employees, loading, error, addEmployee, updateEmployee, deleteEmployee, sendInvite, copyInviteLink, reload: reloadEmployees };
}
