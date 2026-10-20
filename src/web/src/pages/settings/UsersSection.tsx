import { Anchor, Box, Group, Stack, Text, TextInput, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import {
  getListInvitationsQueryKey,
  useInvite,
  useListCompanyUsers,
  useListInvitations,
  useRevokeInvitation,
} from '../../api/generated/account/account'
import type { CurrentUserResponse, InvitationDto } from '../../api/generated/model'
import { SaveButton } from '../../components/feedback'
import { formatDateTime } from '../../lib/format'
import { useSaved } from '../../lib/useSaved'
import { useShake } from '../../lib/useShake'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }
const rowStyle = { borderTop: '1px solid var(--z-line-soft)' }

/** A company has a handful of users; one page of the API's paged lists holds them all. */
const LIST = { page: 1, pageSize: 100 }

/**
 * The company's users, the invitations sent, and a form to invite someone by email. Without roles in
 * v1 everyone invited sees everything, purchase prices too; the note says so.
 */
export function UsersSection({ user }: { user: CurrentUserResponse }) {
  const { t } = useTranslation()
  const users = useListCompanyUsers(LIST)
  const invitations = useListInvitations(LIST)
  const title = t('settings.users.title')

  return (
    <Box component="section" aria-label={title} p="lg" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Stack gap="md">
        <Stack gap={2}>
          <Title order={2}>{title}</Title>
          <Text fz="sm" c="dimmed">
            {t('settings.users.description')}
          </Text>
        </Stack>

        <Stack gap={0}>
          {users.data?.items.map((u) => (
            <Group key={u.id} justify="space-between" py={10} style={rowStyle} wrap="nowrap">
              <Stack gap={0} miw={0}>
                <Text fw={500} truncate>
                  {u.name}
                  {u.id === user.id && (
                    <Text component="span" c="dimmed" fw={400}>
                      {' '}
                      {t('settings.users.you')}
                    </Text>
                  )}
                </Text>
                <Text fz="xs" c="dimmed" truncate>
                  {u.email}
                </Text>
              </Stack>
            </Group>
          ))}
          {invitations.data?.items.map((invitation) => <InvitationRow key={invitation.id} invitation={invitation} />)}
        </Stack>

        <InviteForm />
      </Stack>
    </Box>
  )
}

function InvitationRow({ invitation }: { invitation: InvitationDto }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: getListInvitationsQueryKey() })
  const resend = useInvite({ mutation: { onSuccess: refresh } })
  const revoke = useRevokeInvitation({ mutation: { onSuccess: refresh } })
  const expired = invitation.state === 'expired'
  const error = formErrorMessage(t, resend.error ?? revoke.error)

  return (
    <Group justify="space-between" py={10} style={rowStyle} wrap="wrap" gap="sm">
      <Stack gap={0} miw={0}>
        <Text truncate>{invitation.email}</Text>
        <Text fz="xs" c={expired ? 'var(--z-status-low)' : 'dimmed'}>
          {expired
            ? t('settings.users.expired')
            : t('settings.users.pending', { date: formatDateTime(invitation.sentAt, i18n.language) })}
        </Text>
        {error && (
          <Text role="alert" fz="xs" c="var(--mantine-color-error)">
            {error}
          </Text>
        )}
      </Stack>
      <Group gap="md">
        <Anchor
          component="button"
          type="button"
          fz="sm"
          disabled={resend.isPending}
          onClick={() => resend.mutate({ data: { email: invitation.email } })}
        >
          {resend.isSuccess ? t('settings.users.resent') : t('settings.users.resend')}
        </Anchor>
        <Anchor
          component="button"
          type="button"
          fz="sm"
          c="dimmed"
          disabled={revoke.isPending}
          onClick={() => revoke.mutate({ id: invitation.id })}
          aria-label={t('settings.users.revokeLabel', { email: invitation.email })}
        >
          {t('settings.users.revoke')}
        </Anchor>
      </Group>
    </Group>
  )
}

function InviteForm() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  const [sentTo, setSentTo] = useState<string | null>(null)
  const saved = useSaved()
  const shake = useShake()
  const invite = useInvite({
    mutation: {
      onSuccess: async (invitation) => {
        setSentTo(invitation.email)
        setEmail('')
        await queryClient.invalidateQueries({ queryKey: getListInvitationsQueryKey() })
        await saved.confirm()
      },
      onError: shake.shake,
    },
  })
  const fieldErrors = fieldErrorMessages(t, invite.error)
  const formError = formErrorMessage(t, invite.error)

  return (
    <form
      noValidate
      className={shake.className}
      onSubmit={(event: SubmitEvent<HTMLFormElement>) => {
        event.preventDefault()
        setSentTo(null)
        invite.mutate({ data: { email } })
      }}
    >
      <Stack gap="sm" maw={480}>
        <TextInput
          label={t('settings.users.inviteEmail')}
          type="email"
          autoComplete="off"
          value={email}
          onChange={(e) => setEmail(e.currentTarget.value)}
          error={fieldErrors.email}
          description={t('settings.users.inviteNote')}
        />
        {formError && (
          <Text role="alert" fz="sm" c="var(--mantine-color-error)">
            {formError}
          </Text>
        )}
        {sentTo && (
          <Text role="status" fz="sm" c="var(--z-accent-text)">
            {t('settings.users.sent', { email: sentTo })}
          </Text>
        )}
        <Group>
          <SaveButton type="submit" loading={invite.isPending} saved={saved.saved}>
            {t('settings.users.invite')}
          </SaveButton>
        </Group>
      </Stack>
    </form>
  )
}
