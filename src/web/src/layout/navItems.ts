import { IconBox, IconBuildingCommunity, IconChartBar, IconHome, IconLink, IconReceipt, IconSettings, IconTruckDelivery } from '@tabler/icons-react'
import type { ComponentType } from 'react'
import type { Permissions } from '../auth/permissions'

export interface NavItem {
  to: string
  labelKey: string
  icon: ComponentType<{ size?: number; stroke?: number; className?: string }>
  /** Shown only to users with this permission. */
  requires?: keyof Permissions
}

// Only screens that exist are listed; new ones are added as they are built.
export const navItems: NavItem[] = [
  { to: '/', labelKey: 'common.nav.home', icon: IconHome },
  { to: '/items', labelKey: 'common.nav.items', icon: IconBox },
  { to: '/orders', labelKey: 'common.nav.orders', icon: IconReceipt },
  { to: '/receipts', labelKey: 'common.nav.receipts', icon: IconTruckDelivery },
  { to: '/reports', labelKey: 'common.nav.reports', icon: IconChartBar, requires: 'manage' },
  { to: '/channels', labelKey: 'common.nav.channels', icon: IconLink },
  { to: '/settings', labelKey: 'common.nav.settings', icon: IconSettings },
  { to: '/platform', labelKey: 'common.nav.platform', icon: IconBuildingCommunity, requires: 'platform' },
]

/** The screens the user's role may open. */
export function navItemsFor(permissions: Permissions) {
  return navItems.filter((item) => !item.requires || permissions[item.requires])
}

/** On a phone, the screens in the bottom bar; the rest are under "More". */
export const PHONE_TABS = 3
