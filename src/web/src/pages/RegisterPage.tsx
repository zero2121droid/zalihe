import { Anchor, Button, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type ChangeEvent, type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { fieldErrorMessages, formErrorMessage } from '../api/errors'
import { getGetCurrentUserQueryKey, useRegister } from '../api/generated/auth/auth'
import type { RegisterRequest } from '../api/generated/model'

type Fields = Omit<RegisterRequest, 'language'>

export function RegisterPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [fields, setFields] = useState<Fields>({ companyName: '', name: '', email: '', password: '' })

  const register = useRegister({
    mutation: {
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() })
        void navigate('/', { replace: true })
      },
    },
  })

  const fieldErrors = fieldErrorMessages(t, register.error)
  const formError = formErrorMessage(t, register.error)

  function bind(field: keyof Fields) {
    return {
      value: fields[field],
      onChange: (e: ChangeEvent<HTMLInputElement>) => {
        const value = e.currentTarget.value
        setFields((current) => ({ ...current, [field]: value }))
      },
      error: fieldErrors[field],
    }
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    // The language chosen on this page becomes the user's saved language.
    register.mutate({ data: { ...fields, language: i18n.language } })
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="md">
        <Stack gap={4}>
          <Title order={1} fz={22}>
            {t('auth.register.title')}
          </Title>
          <Text c="dimmed">{t('auth.register.subtitle')}</Text>
        </Stack>

        <TextInput label={t('auth.register.companyName')} autoComplete="organization" {...bind('companyName')} />
        <TextInput label={t('auth.register.name')} autoComplete="name" {...bind('name')} />
        <TextInput label={t('auth.register.email')} type="email" autoComplete="email" {...bind('email')} />
        <PasswordInput
          label={t('auth.register.password')}
          description={t('auth.register.passwordHint')}
          autoComplete="new-password"
          {...bind('password')}
        />

        {formError && (
          <Text role="alert" c="var(--mantine-color-error)" fz="sm">
            {formError}
          </Text>
        )}

        <Button type="submit" fullWidth loading={register.isPending}>
          {t('auth.register.submit')}
        </Button>

        <Text fz="sm" c="dimmed" ta="center">
          {t('auth.register.haveAccount')}{' '}
          <Anchor component={Link} to="/login" fz="sm">
            {t('auth.register.toLogin')}
          </Anchor>
        </Text>
      </Stack>
    </form>
  )
}
