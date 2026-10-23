import { Drawer, NavLink, Stack, UnstyledButton } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconDots } from '@tabler/icons-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink as RouterNavLink, useLocation } from 'react-router'
import classes from './AppLayout.module.css'
import type { NavItem } from './navItems'

/**
 * The menu on a phone (DESIGN.md "Telefon i tablet"): a bar at the bottom, in reach of a thumb, with
 * the screens used every day; "More" opens the rest from the bottom, with the account under them.
 */
export function PhoneTabs({ tabs, more, account }: { tabs: NavItem[]; more: NavItem[]; account: ReactNode }) {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const [opened, { open, close }] = useDisclosure()
  const inMore = more.some((item) => pathname.startsWith(item.to))

  return (
    <>
      <nav className={classes.tabs} aria-label={t('common.nav.phone')}>
        {tabs.map(({ to, labelKey, icon: Icon }) => (
          <RouterNavLink key={to} to={to} end={to === '/'} className={`z-focus ${classes.tab}`}>
            <Icon size={20} stroke={1.8} className={classes.tabIcon} />
            {t(labelKey)}
          </RouterNavLink>
        ))}
        <UnstyledButton
          className={`z-focus ${classes.tab}`}
          data-current={inMore || undefined}
          aria-expanded={opened}
          onClick={open}
        >
          <IconDots size={20} stroke={1.8} className={classes.tabIcon} />
          {t('common.nav.more')}
        </UnstyledButton>
      </nav>

      <Drawer opened={opened} onClose={close} position="bottom" size="auto" title={t('common.nav.more')}>
        <Stack gap="md" pb="md">
          <Stack gap={2}>
            {more.map(({ to, labelKey, icon: Icon }) => (
              <NavLink
                key={to}
                component={RouterNavLink}
                to={to}
                onClick={close}
                className={classes.navLink}
                label={t(labelKey)}
                leftSection={<Icon size={18} stroke={1.8} className={classes.navIcon} />}
              />
            ))}
          </Stack>
          {account}
        </Stack>
      </Drawer>
    </>
  )
}
