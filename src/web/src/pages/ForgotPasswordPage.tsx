import { Anchor, Button, Stack, Text, TextInput, Title } from '@mantine/core'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { fieldErrorMessages, formErrorMessage } from '../api/errors'
import { useForgotPassword } from '../api/generated/auth/auth'

/**
 * "Forgot password" (/forgot-password): sends a link for a new password. The answer is the same
 * whether the address has an account or not, so the page never says which one it was.
 */
export function ForgotPasswordPage() {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const forgot = useForgotPassword()

  const fieldErrors = fieldErrorMessages(t, forgot.error)
  const formError = formErrorMessage(t, forgot.error)

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    forgot.mutate({ data: { email } })
  }

  if (forgot.isSuccess) {
    return (
      <Stack gap="sm">
        <Title order={1} className="z-auth-title">
          {t('auth.forgot.sentTitle')}
        </Title>
        <Text c="dimmed">{t('auth.forgot.sentText', { email: email.trim() })}</Text>
        <Button component={Link} to="/login" variant="default" style={{ alignSelf: 'flex-start' }}>
          {t('auth.forgot.toLogin')}
        </Button>
      </Stack>
    )
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="md">
        <Stack gap={4}>
          <Title order={1} className="z-auth-title">
            {t('auth.forgot.title')}
          </Title>
          <Text c="dimmed">{t('auth.forgot.subtitle')}</Text>
        </Stack>

        <TextInput
          label={t('auth.login.email')}
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.currentTarget.value)}
          error={fieldErrors.email}
        />

        {formError && (
          <Text role="alert" c="var(--mantine-color-error)" fz="sm">
            {formError}
          </Text>
        )}

        <Button type="submit" fullWidth loading={forgot.isPending}>
          {t('auth.forgot.submit')}
        </Button>

        <Text fz="sm">
          <Anchor component={Link} to="/login" fz="sm">
            {t('auth.forgot.toLogin')}
          </Anchor>
        </Text>
      </Stack>
    </form>
  )
}
