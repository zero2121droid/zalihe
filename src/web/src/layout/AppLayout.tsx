import { ActionIcon, AppShell, Box, Group, NavLink, Stack, Text, UnstyledButton } from '@mantine/core'
import { useDisclosure, useHotkeys } from '@mantine/hooks'
import { IconLogout, IconMessage2, IconSearch } from '@tabler/icons-react'
import { useQueryClient } from '@tanstack/react-query'
import { type CSSProperties, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink as RouterNavLink, Outlet, ScrollRestoration, useNavigate } from 'react-router'
import { useLogout } from '../api/generated/auth/auth'
import { usePermissions } from '../auth/permissions'
import { useCurrentUser } from '../auth/useCurrentUser'
import { FeedbackModal } from '../components/FeedbackModal'
import { Logo } from '../components/Logo'
import { QuickSearch } from '../components/QuickSearch'
import { ThemeToggle } from '../components/ThemeToggle'
import { quickSearchShortcut } from '../lib/shortcut'
import { useSlidingBox } from '../lib/useSlidingBox'
import classes from './AppLayout.module.css'
import { ChannelAlert } from './ChannelAlert'
import { navItemsFor, PHONE_TABS } from './navItems'
import { usePageWidth } from './pageWidth'
import { PhoneTabs } from './PhoneTabs'

export function AppLayout() {
  const { t } = useTranslation()
  const [searching, search] = useDisclosure()
  const [writing, feedback] = useDisclosure()
  // Also from inside a field: Ctrl+K never types anything.
  useHotkeys([['mod+K', search.open]], [])
  const { user } = useCurrentUser()
  const items = navItemsFor(usePermissions())
  const pageWidth = usePageWidth()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  // The current page's background slides to the new item instead of jumping (DESIGN.md "Pokret").
  const nav = useRef<HTMLDivElement>(null)
  const current = useSlidingBox(nav, '[aria-current="page"]')
  const logout = useLogout({
    mutation: {
      onSettled: () => {
        queryClient.clear()
        void navigate('/login', { replace: true })
      },
    },
  })

  // The company, the user, the theme and signing out: under the menu, and under "More" on a phone.
  const account = (
    <Group className={classes.account} justify="space-between" wrap="nowrap" gap="xs">
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
          aria-label={t('common.logout')}
          title={t('common.logout')}
          loading={logout.isPending}
          onClick={() => logout.mutate()}
        >
          <IconLogout size={16} stroke={1.8} />
        </ActionIcon>
      </Group>
    </Group>
  )

  // Under the menu, so it is on every screen without competing with the screen's own actions (SPEC 11.10).
  const sendFeedback = (
    <UnstyledButton className={`z-focus ${classes.feedback}`} onClick={feedback.open}>
      <IconMessage2 size={16} stroke={1.8} className={classes.feedbackIcon} />
      {t('feedback.open')}
    </UnstyledButton>
  )

  return (
    <AppShell
      // On a phone the menu is the bar at the bottom (PhoneTabs), so the side menu never opens there.
      navbar={{ width: 232, breakpoint: 'sm', collapsed: { mobile: true } }}
      header={{ height: { base: 56, sm: 0 } }}
      footer={{ height: { base: 64, sm: 0 } }}
    >
      <AppShell.Header hiddenFrom="sm" className={classes.header}>
        <Group h="100%" px="md" gap="sm">
          <Logo />
          <ActionIcon ml="auto" size={44} onClick={search.open} aria-label={t('common.quickSearch.open')}>
            <IconSearch size={20} stroke={1.8} />
          </ActionIcon>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar className={classes.navbar}>
        <Stack h="100%" gap={28}>
          <Stack gap="md" visibleFrom="sm">
            <Box px={8}>
              <Logo />
            </Box>
            <UnstyledButton className={`z-focus ${classes.find}`} onClick={search.open}>
              <IconSearch size={16} stroke={1.8} />
              {t('common.quickSearch.open')}
              <span className={classes.kbd}>{quickSearchShortcut}</span>
            </UnstyledButton>
          </Stack>

          <Stack ref={nav} component="nav" aria-label={t('common.nav.main')} gap={2} pos="relative">
            {current.box && (
              <span
                className={classes.navCurrent}
                data-placed={current.placed || undefined}
                style={{ '--y': `${current.box.y}px`, height: current.box.height } as CSSProperties}
                aria-hidden
              />
            )}
            {items.map(({ to, labelKey, icon: Icon }) => (
              <NavLink
                key={to}
                component={RouterNavLink}
                to={to}
                // Home matches only itself; other items stay active on their sub-pages (/items/123).
                end={to === '/'}
                className={classes.navLink}
                label={t(labelKey)}
                leftSection={<Icon size={16} stroke={1.8} className={classes.navIcon} />}
              />
            ))}
          </Stack>

          <Stack mt="auto" gap="xs">
            {sendFeedback}
            {account}
          </Stack>
        </Stack>
      </AppShell.Navbar>

      <AppShell.Footer hiddenFrom="sm" className={classes.footer}>
        <PhoneTabs tabs={items.slice(0, PHONE_TABS)} more={items.slice(PHONE_TABS)} account={account} feedback={sendFeedback} />
      </AppShell.Footer>

      {searching && <QuickSearch onClose={search.close} />}
      <FeedbackModal opened={writing} onClose={feedback.close} />

      <AppShell.Main>
        <Box className={classes.main} data-width={pageWidth}>
          <ChannelAlert />
          <Outlet />
        </Box>
      </AppShell.Main>

      {/* A new screen starts at the top; Back returns to where the previous one was left. */}
      <ScrollRestoration />
    </AppShell>
  )
}
