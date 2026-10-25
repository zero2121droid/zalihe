import type { UserRole } from '../api/generated/model'
import { useCurrentUser } from './useCurrentUser'

export interface Permissions {
  /** Company settings, users and their roles, connecting the shop: admins. */
  manageCompany: boolean
  /** Imports, reports, checking the shop and stock maintenance: admins and managers. */
  manage: boolean
}

/**
 * What a role may do (SPEC 11.2). The API enforces the same rules; the screens only leave out what it
 * would refuse.
 */
export function permissionsFor(role: UserRole | undefined): Permissions {
  return {
    manageCompany: role === 'admin',
    manage: role === 'admin' || role === 'manager',
  }
}

export function usePermissions(): Permissions {
  return permissionsFor(useCurrentUser().user?.role)
}
