import { Anchor, Button, Checkbox, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate } from 'react-router'
import { fieldErrorMessages, formErrorMessage } from '../api/errors'
import { getGetCurrentUserQueryKey, useLogin } from '../api/generated/auth/auth'

export function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(false)

  const login = useLogin({
    mutation: {
      onSuccess: async () => {
        // Loads the user and a fresh antiforgery token for the new session.
        await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() })
        const from = (location.state as { from?: string } | null)?.from ?? '/'
        void navigate(from, { replace: true })
      },
    },
  })

  const fieldErrors = fieldErrorMessages(t, login.error)
  const formError = formErrorMessage(t, login.error)

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    login.mutate({ data: { email, password, rememberMe } })
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="md">
        <Stack gap={4}>
          <Title order={1} fz={22}>
            {t('auth.login.title')}
          </Title>
          <Text c="dimmed">{t('auth.login.subtitle')}</Text>
        </Stack>

        <TextInput
          label={t('auth.login.email')}
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.currentTarget.value)}
          error={fieldErrors.email}
        />
        <PasswordInput
          label={t('auth.login.password')}
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.currentTarget.value)}
          error={fieldErrors.password}
        />
        <Checkbox
          label={t('auth.login.rememberMe')}
          checked={rememberMe}
          onChange={(e) => setRememberMe(e.currentTarget.checked)}
        />

        {formError && (
          <Text role="alert" c="var(--mantine-color-error)" fz="sm">
            {formError}
          </Text>
        )}

        <Button type="submit" fullWidth loading={login.isPending}>
          {t('auth.login.submit')}
        </Button>

        <Text fz="sm" c="dimmed" ta="center">
          {t('auth.login.noAccount')}{' '}
          <Anchor component={Link} to="/register" fz="sm">
            {t('auth.login.toRegister')}
          </Anchor>
        </Text>
      </Stack>
    </form>
  )
}
