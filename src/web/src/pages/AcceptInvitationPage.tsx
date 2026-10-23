import { Button, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { fieldErrorMessages, formErrorMessage } from '../api/errors'
import { useAcceptInvitation, useGetInvitation } from '../api/generated/invitations/invitations'
import { PageLoader } from '../components/loaders'
import { useLoadingDisplay } from '../lib/useLoadingDisplay'

/**
 * The page an invitation email links to (/invite/:token): the invited person picks a name and a
 * password and is signed in to the inviting company. Open to anyone with the link, signed in or not.
 */
export function AcceptInvitationPage() {
  const { t, i18n } = useTranslation()
  const { token = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')

  const invitation = useGetInvitation(token, { query: { retry: false } })
  const display = useLoadingDisplay(invitation.isPending)
  const accept = useAcceptInvitation({
    mutation: {
      onSuccess: async () => {
        // Someone else may have been signed in in this browser: start clean as the new user.
        queryClient.clear()
        await navigate('/', { replace: true })
      },
    },
  })

  if (invitation.isPending || display !== 'content') return display === 'loader' ? <PageLoader /> : null

  if (invitation.isError) {
    return (
      <Stack gap="sm">
        <Title order={1} className="z-auth-title">
          {t('auth.invitation.invalidTitle')}
        </Title>
        <Text c="dimmed">{t('auth.invitation.invalidText')}</Text>
        <Button component={Link} to="/login" variant="default" style={{ alignSelf: 'flex-start' }}>
          {t('auth.invitation.toLogin')}
        </Button>
      </Stack>
    )
  }

  const info = invitation.data
  const fieldErrors = fieldErrorMessages(t, accept.error)
  const formError = formErrorMessage(t, accept.error)

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    // The language chosen on this page becomes the user's saved language.
    accept.mutate({ token, data: { name, password, language: i18n.language } })
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="md">
        <Stack gap={4}>
          <Title order={1} className="z-auth-title">
            {t('auth.invitation.title', { company: info.companyName })}
          </Title>
          <Text c="dimmed">{t('auth.invitation.subtitle', { name: info.invitedBy, company: info.companyName })}</Text>
        </Stack>

        <Stack gap={2}>
          <Text fz="sm" fw={500}>
            {t('auth.register.email')}
          </Text>
          <Text>{info.email}</Text>
        </Stack>
        <TextInput
          label={t('auth.register.name')}
          autoComplete="name"
          value={name}
          onChange={(e) => setName(e.currentTarget.value)}
          error={fieldErrors.name}
        />
        <PasswordInput
          label={t('auth.register.password')}
          autoComplete="new-password"
          description={t('auth.register.passwordHint')}
          value={password}
          onChange={(e) => setPassword(e.currentTarget.value)}
          error={fieldErrors.password}
        />
        {formError && (
          <Text role="alert" fz="sm" c="var(--mantine-color-error)">
            {formError}
          </Text>
        )}
        <Button type="submit" loading={accept.isPending}>
          {t('auth.invitation.submit')}
        </Button>
      </Stack>
    </form>
  )
}
