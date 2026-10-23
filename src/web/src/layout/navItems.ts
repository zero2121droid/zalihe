import { IconBox, IconChartBar, IconHome, IconLink, IconReceipt, IconSettings } from '@tabler/icons-react'
import type { ComponentType } from 'react'

export interface NavItem {
  to: string
  labelKey: string
  icon: ComponentType<{ size?: number; stroke?: number; className?: string }>
}

// Only screens that exist are listed; new ones are added as they are built.
export const navItems: NavItem[] = [
  { to: '/', labelKey: 'common.nav.home', icon: IconHome },
  { to: '/items', labelKey: 'common.nav.items', icon: IconBox },
  { to: '/orders', labelKey: 'common.nav.orders', icon: IconReceipt },
  { to: '/reports', labelKey: 'common.nav.reports', icon: IconChartBar },
  { to: '/channels', labelKey: 'common.nav.channels', icon: IconLink },
  { to: '/settings', labelKey: 'common.nav.settings', icon: IconSettings },
]

/** On a phone, the screens in the bottom bar; the rest are under "More". */
export const PHONE_TABS = 3
