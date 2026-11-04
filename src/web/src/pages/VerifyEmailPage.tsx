import { Anchor, Button, Group, PinInput, Stack, Text, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { fieldErrorMessages, formErrorMessage } from '../api/errors'
import {
  getGetCurrentUserQueryKey,
  useConfirmRegistration,
  useResendRegistrationCode,
} from '../api/generated/auth/auth'
import feedbackClasses from '../components/feedback.module.css'
import { useShake } from '../lib/useShake'

const CODE_LENGTH = 6
/** The API decides when a new code may go out; this only shows the wait. */
const RESEND_SECONDS = 60
/** How long "Sent" stays after sending a new code. */
const RESENT_SECONDS = 2

/** What the registration form hands over. It lives in the browser's history, so reloading keeps it. */
export interface VerifyEmailState {
  email: string
  token: string
}

/**
 * The second step of registering (/verify-email): the code from the email creates the company.
 * Without the form's state there is nothing to confirm, so it goes back to the form.
 */
export function VerifyEmailPage() {
  const state = useLocation().state as VerifyEmailState | null
  return state ? <CodeForm {...state} /> : <Navigate to="/register" replace />
}

function CodeForm({ email, token }: VerifyEmailState) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const shake = useShake()
  const [code, setCode] = useState('')
  const [secondsLeft, setSecondsLeft] = useState(RESEND_SECONDS)

  useEffect(() => {
    if (secondsLeft === 0) return
    const timer = setTimeout(() => setSecondsLeft((seconds) => seconds - 1), 1000)
    return () => clearTimeout(timer)
  }, [secondsLeft])

  const confirm = useConfirmRegistration({
    mutation: {
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() })
        void navigate('/', { replace: true })
      },
      onError: () => {
        setCode('')
        shake.shake()
      },
    },
  })

  const resend = useResendRegistrationCode({
    mutation: {
      onSuccess: () => {
        setCode('')
        setSecondsLeft(RESEND_SECONDS)
      },
    },
  })

  const error = confirm.error ?? resend.error
  const fieldErrors = fieldErrorMessages(t, error)
  const message = fieldErrors.code ?? fieldErrors.email ?? formErrorMessage(t, error)
  const resent = resend.isSuccess && secondsLeft > RESEND_SECONDS - RESENT_SECONDS

  function submit(value: string) {
    resend.reset()
    confirm.mutate({ data: { token, code: value } })
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    submit(code)
  }

  function handleResend() {
    confirm.reset()
    resend.mutate({ data: { token } })
  }

  return (
    <form onSubmit={handleSubmit} noValidate className={shake.className}>
      <Stack gap="md">
        <Stack gap={4}>
          <Title order={1} className="z-auth-title">
            {t('auth.verify.title')}
          </Title>
          <Text c="dimmed">{t('auth.verify.subtitle', { email })}</Text>
        </Stack>

        <Stack gap={4}>
          <Text fz="sm" fw={500}>
            {t('auth.verify.code')}
          </Text>
          <PinInput
            length={CODE_LENGTH}
            type="number"
            size="lg"
            gap={8}
            autoFocus
            oneTimeCode
            ariaLabel={t('auth.verify.code')}
            value={code}
            onChange={setCode}
            onComplete={submit}
            error={Boolean(message)}
            disabled={confirm.isPending}
            styles={{
              root: { width: '100%' },
              pinInput: { flex: 1, minWidth: 0, width: 'auto' },
              input: { width: '100%', fontFamily: 'var(--mantine-font-family-monospace)' },
            }}
          />
          <Text fz="xs" c="dimmed">
            {t('auth.verify.hint')}
          </Text>
        </Stack>

        {message && (
          <Text role="alert" c="var(--mantine-color-error)" fz="sm">
            {message}
          </Text>
        )}

        <Button type="submit" fullWidth loading={confirm.isPending} disabled={code.length < CODE_LENGTH}>
          {t('auth.verify.submit')}
        </Button>

        <Group justify="space-between" gap="md">
          <Anchor component="button" type="button" fz="sm" disabled={secondsLeft > 0 || resend.isPending} onClick={handleResend}>
            {resent ? (
              <Group component="span" gap={4} display="inline-flex" role="status">
                <svg className={feedbackClasses.check} viewBox="0 0 16 16" aria-hidden>
                  <polyline points="3,8.5 6.5,12 13,4.5" />
                </svg>
                {t('auth.verify.resent')}
              </Group>
            ) : secondsLeft > 0 ? (
              t('auth.verify.resendIn', { seconds: secondsLeft })
            ) : (
              t('auth.verify.resend')
            )}
          </Anchor>
          <Anchor component={Link} to="/register" fz="sm">
            {t('auth.verify.changeEmail')}
          </Anchor>
        </Group>
      </Stack>
    </form>
  )
}
