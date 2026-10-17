import type { AppUser } from '@/types';
import { UserStatus, type UserDTO } from '@/lib/apiClient';

/**
 * Maps a backend UserDTO to the frontend AppUser shape.
 * - Name/color come from the nested `employee` (null until the invite is accepted);
 *   name falls back to the email when there is no employee record yet.
 * - Role: Admin=0, Manager=1 → 'admin'; Employee=2 → 'employee'
 * - isAdmin: solo Admin=0. Manager rientra in role 'admin' per calendario/dipendenti,
 *   ma il backend non gli dà il bypass su accetta/rifiuta/annulla swap request altrui.
 * - firstLoginCompleted: Active(2) or Disabled(3) mean the first login is done
 * - telegramLinked: not yet exposed in UserDTO, defaults to false
 */
export function mapUserDTO(dto: UserDTO): AppUser {
	const emp = dto.employee;
	return {
		id: dto.id,
		name: emp ? `${emp.name} ${emp.surname}`.trim() : dto.email,
		email: dto.email,
		role: dto.role <= 1 ? 'admin' : 'employee',
		isAdmin: dto.role === 0,
		color: emp?.color ?? '#6366f1',
		firstLoginCompleted: dto.status >= UserStatus.Active,
		telegramLinked: false,
	};
}
