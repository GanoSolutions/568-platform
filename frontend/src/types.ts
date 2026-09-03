export interface Employee {
  id: string
  name: string
  color: string
  email?: string
  role?: string
}

export interface EmployeeDetail extends Employee {
  /** Solo cognome — `name` resta il nome completo per la visualizzazione. */
  surname: string
  fiscalCode: string
  phone: string
  contractEnd: string
  invited: boolean
  firstLoginCompleted: boolean
  /** Account disabilitato (es. fine contratto) — non selezionabile per nuovi turni. */
  disabled: boolean
}

export interface ShiftEmployee {
  id: string
  shiftId: string
  startTime: string
  endTime: string
  overnight: boolean
}

export interface ShiftData {
  closed: boolean
  employees: ShiftEmployee[]
}

export interface AppUser {
  id: string
  authUserId?: string
  name: string
  email: string
  role: 'admin' | 'employee'
  /** Vero Admin backend (ruolo 0), distinto da Manager: Manager rientra in role 'admin' per calendario/dipendenti, ma il backend non gli concede il bypass su accetta/rifiuta swap request altrui. */
  isAdmin: boolean
  color: string
  firstLoginCompleted: boolean
  telegramLinked?: boolean
}

export interface SwapRequest {
  id: string
  shiftId: string
  requesterId: string
  targetEmployeeId: string
  status: 'pending' | 'accepted' | 'rejected' | 'cancelled'
  createdAt: string
  respondedAt: string | null
  /** Data del turno (YYYY-MM-DD), risolta lato client via shiftApi.getById. */
  workDate: string
  /** updatedAt del turno collegato — confrontato con createdAt per segnalare se il turno è cambiato dopo la richiesta. */
  shiftUpdatedAt: string
}
