import { ActionIcon, AppShell, Box, Burger, Group, NavLink, Stack, Text } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconBox, IconHome, IconLogout } from '@tabler/icons-react'
import { useQueryClient } from '@tanstack/react-query'
import type { ComponentType } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink as RouterNavLink, Outlet, useNavigate } from 'react-router'
import { useLogout } from '../api/generated/auth/auth'
import { useCurrentUser } from '../auth/useCurrentUser'
import { Logo } from '../components/Logo'
import { ThemeToggle } from '../components/ThemeToggle'
import classes from './AppLayout.module.css'
import { usePageWidth } from './pageWidth'

interface NavItem {
  to: string
  labelKey: string
  icon: ComponentType<{ size?: number; stroke?: number; className?: string }>
}

// Only screens that exist are listed; new ones are added as they are built.
const navItems: NavItem[] = [
  { to: '/', labelKey: 'common.nav.home', icon: IconHome },
  { to: '/items', labelKey: 'common.nav.items', icon: IconBox },
]

export function AppLayout() {
  const { t } = useTranslation()
  const [opened, { toggle, close }] = useDisclosure()
  const { user } = useCurrentUser()
  const pageWidth = usePageWidth()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const logout = useLogout({
    mutation: {
      onSettled: () => {
        queryClient.clear()
        void navigate('/login', { replace: true })
      },
    },
  })

  return (
    <AppShell
      navbar={{ width: 232, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      header={{ height: { base: 56, sm: 0 } }}
    >
      <AppShell.Header hiddenFrom="sm" className={classes.header}>
        <Group h="100%" px="md" gap="sm">
          <Burger opened={opened} onClick={toggle} size="sm" aria-label={t('common.nav.openMenu')} />
          <Logo />
        </Group>
      </AppShell.Header>

      <AppShell.Navbar className={classes.navbar}>
        <Stack h="100%" gap={28}>
          <Box px={8} visibleFrom="sm">
            <Logo />
          </Box>

          <Stack component="nav" aria-label={t('common.nav.main')} gap={2}>
            {navItems.map(({ to, labelKey, icon: Icon }) => (
              <NavLink
                key={to}
                component={RouterNavLink}
                to={to}
                // Home matches only itself; other items stay active on their sub-pages (/items/123).
                end={to === '/'}
                onClick={close}
                className={classes.navLink}
                label={t(labelKey)}
                leftSection={<Icon size={16} stroke={1.8} className={classes.navIcon} />}
              />
            ))}
          </Stack>

          <Group mt="auto" className={classes.account} justify="space-between" wrap="nowrap" gap="xs">
            <Stack gap={2} miw={0}>
              <Text fw={500} truncate>
                {user?.tenantName}
              </Text>
              <Text fz="xs" c="dimmed" truncate>
                {user?.name}
              </Text>
            </Stack>
            <Group gap={2} wrap="nowrap">
              <ThemeToggle />
              <ActionIcon
                size={36}
                aria-label={t('common.logout')}
                title={t('common.logout')}
                loading={logout.isPending}
                onClick={() => logout.mutate()}
              >
                <IconLogout size={16} stroke={1.8} />
              </ActionIcon>
            </Group>
          </Group>
        </Stack>
      </AppShell.Navbar>

      <AppShell.Main>
        <Box className={classes.main} data-width={pageWidth}>
          <Outlet />
        </Box>
      </AppShell.Main>
    </AppShell>
  )
}
