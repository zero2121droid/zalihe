import { Box, Group } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { Outlet, ScrollRestoration } from 'react-router'
import { LanguageSwitch } from '../components/LanguageSwitch'
import { Logo, LogoMark } from '../components/Logo'
import { ThemeToggle } from '../components/ThemeToggle'
import classes from './AuthLayout.module.css'

/**
 * Sign-in, registration and the other pages before the app: the form on the left, the logo and name
 * large on the right; on a phone the usual logo sits above the form instead.
 */
export function AuthLayout() {
  const { t } = useTranslation()
  return (
    <div className={classes.page}>
      <div className={classes.top}>
        <div className={classes.small}>
          <Logo />
        </div>
        <Group gap="xs">
          <LanguageSwitch />
          <ThemeToggle />
        </Group>
      </div>
      <div className={classes.body}>
        <Box className={classes.form}>
          <Outlet />
          <ScrollRestoration />
        </Box>
        <div className={classes.brand} aria-hidden>
          <LogoMark size={96} />
          <span className={classes.name}>{t('common.appName')}</span>
        </div>
      </div>
    </div>
  )
}
