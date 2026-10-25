import { Anchor, Box, Group, Select, Stack, Text, TextInput, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import {
  getListCompanyUsersQueryKey,
  getListInvitationsQueryKey,
  useChangeUserRole,
  useInvite,
  useListCompanyUsers,
  useListInvitations,
  useRevokeInvitation,
} from '../../api/generated/account/account'
import { getGetCurrentUserQueryKey } from '../../api/generated/auth/auth'
import { type CompanyUserDto, type CurrentUserResponse, type InvitationDto, UserRole } from '../../api/generated/model'
import { SaveButton } from '../../components/feedback'
import feedbackClasses from '../../components/feedback.module.css'
import { formatDateTime } from '../../lib/format'
import { useArrivals } from '../../lib/useArrivals'
import { useLeaving } from '../../lib/useLeaving'
import { useSaved } from '../../lib/useSaved'
import { useShake } from '../../lib/useShake'
import classes from './UsersSection.module.css'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }
/** How long "Sent" stays after sending an invitation again. */
const RESENT_MS = 1600

/** A company has a handful of users; one page of the API's paged lists holds them all. */
const LIST = { page: 1, pageSize: 100 }

/** Roles from the most to the least allowed. */
const ROLES = [UserRole.admin, UserRole.manager, UserRole.employee]

/**
 * The company's users with their roles, the invitations sent, and a form to invite someone by email
 * with a role. Only admins get here (SPEC 11.2).
 */
export function UsersSection({ user }: { user: CurrentUserResponse }) {
  const { t } = useTranslation()
  const users = useListCompanyUsers(LIST)
  const invitations = useListInvitations(LIST)
  const title = t('settings.users.title')
  const invitationRows = useLeaving(invitations.data?.items ?? [], (i) => i.id, 'invitations', invitations.isSuccess)
  const arrivals = useArrivals(invitations.data?.items.map((i) => i.id), 'invitations')

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
          {users.data?.items.map((member) => (
            <UserRow key={member.id} member={member} isYou={member.id === user.id} />
          ))}
          {invitationRows.map(({ item: invitation, leaving }) => (
            <div
              key={invitation.id}
              className={[classes.row, leaving && classes.leaving, arrivals.has(invitation.id) && classes.fresh].filter(Boolean).join(' ')}
              aria-hidden={leaving || undefined}
            >
              <div>
                <InvitationRow invitation={invitation} />
              </div>
            </div>
          ))}
        </Stack>

        <InviteForm />
      </Stack>
    </Box>
  )
}

function UserRow({ member, isYou }: { member: CompanyUserDto; isYou: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const change = useChangeUserRole({
    mutation: {
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: getListCompanyUsersQueryKey() })
        // Giving up one's own admin role changes what this screen may show.
        if (isYou) await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() })
      },
    },
  })
  // "user.last_admin" comes for the role field; anything else is about the request.
  const error = fieldErrorMessages(t, change.error).role ?? formErrorMessage(t, change.error)

  return (
    <Group justify="space-between" py={10} className={classes.row} wrap="wrap" gap="sm">
      <Stack gap={0} miw={0} style={{ flex: '1 1 200px' }}>
        <Text fw={500} truncate>
          {member.name}
          {isYou && (
            <Text component="span" c="dimmed" fw={400}>
              {' '}
              {t('settings.users.you')}
            </Text>
          )}
        </Text>
        <Text fz="xs" c="dimmed" truncate>
          {member.email}
        </Text>
        {error && (
          <Text role="alert" fz="xs" c="var(--mantine-color-error)">
            {error}
          </Text>
        )}
      </Stack>
      <RoleSelect
        w={180}
        aria-label={t('settings.users.roleOf', { name: member.name })}
        value={member.role}
        disabled={change.isPending}
        onChange={(role) => role !== member.role && change.mutate({ id: member.id, data: { role } })}
      />
    </Group>
  )
}

function RoleSelect({
  value,
  onChange,
  ...props
}: { value: UserRole; onChange: (role: UserRole) => void; w?: number; label?: string; description?: string; 'aria-label'?: string; disabled?: boolean }) {
  const { t } = useTranslation()
  return (
    <Select
      {...props}
      data={ROLES.map((role) => ({ value: role, label: t(`settings.users.roles.${role}`) }))}
      value={value}
      onChange={(role) => role && onChange(role as UserRole)}
      allowDeselect={false}
    />
  )
}

function InvitationRow({ invitation }: { invitation: InvitationDto }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: getListInvitationsQueryKey() })
  const resend = useInvite({ mutation: { onSuccess: refresh } })
  const resent = useResent(resend.isSuccess, resend.submittedAt)
  const revoke = useRevokeInvitation({ mutation: { onSuccess: refresh } })
  const expired = invitation.state === 'expired'
  const error = formErrorMessage(t, resend.error ?? revoke.error)

  return (
    <Group justify="space-between" py={10} wrap="wrap" gap="sm">
      <Stack gap={0} miw={0}>
        <Text truncate>{invitation.email}</Text>
        <Text fz="xs" c={expired ? 'var(--z-status-low)' : 'dimmed'}>
          {t(`settings.users.roles.${invitation.role}`)}
          {' · '}
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
          onClick={() => resend.mutate({ data: { email: invitation.email, role: invitation.role } })}
        >
          {resent ? (
            <span className={classes.sent} role="status">
              <svg className={feedbackClasses.check} viewBox="0 0 16 16" aria-hidden>
                <polyline points="3,8.5 6.5,12 13,4.5" />
              </svg>
              {t('settings.users.resent')}
            </span>
          ) : (
            t('settings.users.resend')
          )}
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

/** True for a moment after an invitation was sent again (`submittedAt` tells one send from the next). */
function useResent(succeeded: boolean, submittedAt: number) {
  const [overFor, setOverFor] = useState<number | null>(null)

  useEffect(() => {
    if (!succeeded) return
    const timer = setTimeout(() => setOverFor(submittedAt), RESENT_MS)
    return () => clearTimeout(timer)
  }, [succeeded, submittedAt])

  return succeeded && overFor !== submittedAt
}

function InviteForm() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  // The least allowed role by default; the admin chooses more on purpose.
  const [role, setRole] = useState<UserRole>(UserRole.employee)
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
        invite.mutate({ data: { email, role } })
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
        />
        <RoleSelect label={t('settings.users.role')} description={t(`settings.users.roleHints.${role}`)} value={role} onChange={setRole} />
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
