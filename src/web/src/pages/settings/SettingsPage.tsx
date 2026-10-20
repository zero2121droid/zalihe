import { Box, Group, PasswordInput, SegmentedControl, Stack, Text, TextInput, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type ReactNode, type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import { useChangePassword, useUpdateCompany, useUpdateProfile } from '../../api/generated/account/account'
import { getGetCurrentUserQueryKey } from '../../api/generated/auth/auth'
import type { CurrentUserResponse } from '../../api/generated/model'
import { useCurrentUser } from '../../auth/useCurrentUser'
import { SaveButton } from '../../components/feedback'
import { languages } from '../../i18n'
import { useSaved } from '../../lib/useSaved'
import { useShake } from '../../lib/useShake'
import { UsersSection } from './UsersSection'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

/** Settings of the company and of the signed-in user: three small forms, each saved on its own. */
export function SettingsPage() {
  const { t } = useTranslation()
  const { user } = useCurrentUser()

  return (
    <Stack gap="lg">
      <Title order={1} lts="-0.01em">
        {t('settings.title')}
      </Title>
      {user && (
        <>
          <CompanyForm user={user} />
          <UsersSection user={user} />
          <ProfileForm user={user} />
          <PasswordForm />
        </>
      )}
    </Stack>
  )
}

function Section({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <Box component="section" aria-label={title} p="lg" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Stack gap="md">
        <Stack gap={2}>
          <Title order={2}>{title}</Title>
          <Text fz="sm" c="dimmed">
            {description}
          </Text>
        </Stack>
        {children}
      </Stack>
    </Box>
  )
}

/** The form's fields, at most 480 px wide, with its error and save button under them. */
function Form({
  onSubmit,
  shakeClass,
  error,
  saving,
  saved,
  children,
}: {
  onSubmit: () => void
  shakeClass: string | undefined
  error: string | null
  saving: boolean
  saved: boolean
  children: ReactNode
}) {
  const { t } = useTranslation()
  return (
    <form
      noValidate
      className={shakeClass}
      onSubmit={(event: SubmitEvent<HTMLFormElement>) => {
        event.preventDefault()
        onSubmit()
      }}
    >
      <Stack gap="md" maw={480}>
        {children}
        {error && (
          <Text role="alert" fz="sm" c="var(--mantine-color-error)">
            {error}
          </Text>
        )}
        <Group>
          <SaveButton type="submit" loading={saving} saved={saved}>
            {t('settings.save')}
          </SaveButton>
        </Group>
      </Stack>
    </form>
  )
}

/** Puts the answer in place of "me", so the menu, the name and the language change at once. */
function useShowUser() {
  const queryClient = useQueryClient()
  return (user: CurrentUserResponse) => queryClient.setQueryData(getGetCurrentUserQueryKey(), user)
}

function CompanyForm({ user }: { user: CurrentUserResponse }) {
  const { t } = useTranslation()
  const [name, setName] = useState(user.tenantName)
  const saved = useSaved()
  const shake = useShake()
  const showUser = useShowUser()
  const save = useUpdateCompany({
    mutation: {
      onSuccess: async (updated) => {
        showUser(updated)
        setName(updated.tenantName)
        await saved.confirm()
      },
      onError: shake.shake,
    },
  })
  const fieldErrors = fieldErrorMessages(t, save.error)

  return (
    <Section title={t('settings.company.title')} description={t('settings.company.description')}>
      <Form
        onSubmit={() => save.mutate({ data: { name } })}
        shakeClass={shake.className}
        error={formErrorMessage(t, save.error)}
        saving={save.isPending}
        saved={saved.saved}
      >
        <TextInput
          label={t('settings.company.name')}
          autoComplete="organization"
          value={name}
          onChange={(e) => setName(e.currentTarget.value)}
          error={fieldErrors.name}
        />
      </Form>
    </Section>
  )
}

function ProfileForm({ user }: { user: CurrentUserResponse }) {
  const { t } = useTranslation()
  const [name, setName] = useState(user.name)
  const [language, setLanguageChoice] = useState(user.language)
  const saved = useSaved()
  const shake = useShake()
  const showUser = useShowUser()
  const save = useUpdateProfile({
    mutation: {
      onSuccess: async (updated) => {
        // The new language applies through "me" (useCurrentUser), right after saving.
        showUser(updated)
        setName(updated.name)
        await saved.confirm()
      },
      onError: shake.shake,
    },
  })
  const fieldErrors = fieldErrorMessages(t, save.error)

  return (
    <Section title={t('settings.profile.title')} description={t('settings.profile.description')}>
      <Form
        onSubmit={() => save.mutate({ data: { name, language } })}
        shakeClass={shake.className}
        error={formErrorMessage(t, save.error)}
        saving={save.isPending}
        saved={saved.saved}
      >
        <TextInput
          label={t('settings.profile.name')}
          autoComplete="name"
          value={name}
          onChange={(e) => setName(e.currentTarget.value)}
          error={fieldErrors.name}
        />
        {/* Not a field: it can't be changed, so it shouldn't look like one. */}
        <Stack gap={2}>
          <Text fz="sm" fw={500}>
            {t('settings.profile.email')}
          </Text>
          <Text>{user.email}</Text>
          <Text fz="xs" c="dimmed">
            {t('settings.profile.emailNote')}
          </Text>
        </Stack>
        <Stack gap={4}>
          <Text component="span" id="settings-language" fz="sm" fw={500}>
            {t('common.language')}
          </Text>
          <SegmentedControl
            aria-labelledby="settings-language"
            value={language}
            onChange={setLanguageChoice}
            data={languages.map((value) => ({ value, label: t(`common.languages.${value}`) }))}
            style={{ alignSelf: 'flex-start' }}
          />
          {fieldErrors.language && (
            <Text fz="xs" c="var(--mantine-color-error)">
              {fieldErrors.language}
            </Text>
          )}
        </Stack>
      </Form>
    </Section>
  )
}

function PasswordForm() {
  const { t } = useTranslation()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const saved = useSaved()
  const shake = useShake()
  const save = useChangePassword({
    mutation: {
      onSuccess: async () => {
        setCurrentPassword('')
        setNewPassword('')
        await saved.confirm()
      },
      onError: shake.shake,
    },
  })
  const fieldErrors = fieldErrorMessages(t, save.error)

  return (
    <Section title={t('settings.password.title')} description={t('settings.password.description')}>
      <Form
        onSubmit={() => save.mutate({ data: { currentPassword, newPassword } })}
        shakeClass={shake.className}
        error={formErrorMessage(t, save.error)}
        saving={save.isPending}
        saved={saved.saved}
      >
        <PasswordInput
          label={t('settings.password.current')}
          autoComplete="current-password"
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.currentTarget.value)}
          error={fieldErrors.currentPassword}
        />
        <PasswordInput
          label={t('settings.password.new')}
          autoComplete="new-password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.currentTarget.value)}
          error={fieldErrors.newPassword}
          description={t('settings.password.rule')}
        />
      </Form>
    </Section>
  )
}
