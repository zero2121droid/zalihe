import { Center, Loader, Text } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { Navigate, Outlet, useLocation } from 'react-router'
import { formErrorMessage } from '../api/errors'
import { useCurrentUser } from './useCurrentUser'

function FullPageLoader() {
  const { t } = useTranslation()
  return (
    <Center mih="100vh">
      <Loader size="sm" aria-label={t('common.loading')} />
    </Center>
  )
}

/** Renders child routes only for signed-in users; others go to the login page. */
export function RequireAuth() {
  const { t } = useTranslation()
  const location = useLocation()
  const { user, isAnonymous, isPending, error } = useCurrentUser()

  if (isPending) return <FullPageLoader />
  if (isAnonymous) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (!user) {
    return (
      <Center mih="100vh">
        <Text role="alert">{formErrorMessage(t, error)}</Text>
      </Center>
    )
  }
  return <Outlet />
}

/** Login and registration pages; signed-in users are sent into the app. */
export function PublicOnly() {
  const { user, isPending } = useCurrentUser()

  if (isPending) return <FullPageLoader />
  if (user) return <Navigate to="/" replace />
  return <Outlet />
}
