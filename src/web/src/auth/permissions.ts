import type { CurrentUserResponse } from '../api/generated/model'
import { useCurrentUser } from './useCurrentUser'

export interface Permissions {
  /** Company settings, users and their roles, connecting the shop: admins. */
  manageCompany: boolean
  /** Imports, reports, checking the shop and stock maintenance: admins and managers. */
  manage: boolean
  /** Purchase prices and stock value: admins, managers, and employees when the company allows it. */
  seePurchasePrices: boolean
  /** The page about all companies: only the platform's owner (SPEC 11.8), whatever the role. */
  platform: boolean
}

/**
 * What a role may do (SPEC 11.2). The API enforces the same rules; the screens only leave out what it
 * would refuse.
 */
export function permissionsFor(user: Pick<CurrentUserResponse, 'role' | 'canSeePurchasePrices' | 'isPlatformOwner'> | undefined): Permissions {
  const role = user?.role
  return {
    manageCompany: role === 'admin',
    manage: role === 'admin' || role === 'manager',
    seePurchasePrices: user?.canSeePurchasePrices ?? false,
    platform: user?.isPlatformOwner ?? false,
  }
}

export function usePermissions(): Permissions {
  return permissionsFor(useCurrentUser().user)
}
