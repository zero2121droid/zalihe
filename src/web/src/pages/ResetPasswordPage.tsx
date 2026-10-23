import { Button, PasswordInput, Stack, Text, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { ApiError, fieldErrorMessages, formErrorMessage } from '../api/errors'
import { useResetPassword } from '../api/generated/auth/auth'

const RESET_INVALID = 'auth.reset_invalid'

/**
 * The page the reset email links to (/reset-password?email=…&token=…): a new password, then the
 * user is signed in. Open signed in or not, like the invitation link.
 */
export function ResetPasswordPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [password, setPassword] = useState('')

  const reset = useResetPassword({
    mutation: {
      onSuccess: async () => {
        // Someone else may have been signed in in this browser: start clean as this user.
        queryClient.clear()
        await navigate('/', { replace: true })
      },
    },
  })

  const linkInvalid =
    !email || !token || (reset.error instanceof ApiError && reset.error.errors.some((e) => e.code === RESET_INVALID))

  if (linkInvalid) {
    return (
      <Stack gap="sm">
        <Title order={1} className="z-auth-title">
          {t('auth.reset.invalidTitle')}
        </Title>
        <Text c="dimmed">{t('auth.reset.invalidText')}</Text>
        <Button component={Link} to="/forgot-password" variant="default" style={{ alignSelf: 'flex-start' }}>
          {t('auth.reset.requestNew')}
        </Button>
      </Stack>
    )
  }

  const fieldErrors = fieldErrorMessages(t, reset.error)
  const formError = formErrorMessage(t, reset.error)

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    reset.mutate({ data: { email, token, newPassword: password } })
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="md">
        <Stack gap={4}>
          <Title order={1} className="z-auth-title">
            {t('auth.reset.title')}
          </Title>
          <Text c="dimmed">{t('auth.reset.subtitle', { email })}</Text>
        </Stack>

        <PasswordInput
          label={t('auth.reset.password')}
          autoComplete="new-password"
          description={t('auth.register.passwordHint')}
          value={password}
          onChange={(e) => setPassword(e.currentTarget.value)}
          error={fieldErrors.newPassword}
        />

        {formError && (
          <Text role="alert" fz="sm" c="var(--mantine-color-error)">
            {formError}
          </Text>
        )}

        <Button type="submit" loading={reset.isPending}>
          {t('auth.reset.submit')}
        </Button>
      </Stack>
    </form>
  )
}
