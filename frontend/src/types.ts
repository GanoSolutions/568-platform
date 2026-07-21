export interface Employee {
  id: string
  name: string
  color: string
  email?: string
  role?: string
}

export interface EmployeeDetail extends Employee {
  fiscalCode: string
  phone: string
  contractEnd: string
  invited: boolean
  firstLoginCompleted: boolean
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
  workDate: string
  requester: { id: string; name: string; color: string } | null
  target: { id: string; name: string; color: string } | null
}
