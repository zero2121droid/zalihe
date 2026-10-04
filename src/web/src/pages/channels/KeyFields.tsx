import { PasswordInput, TextInput } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import type { Keys } from './keys'

const monospace = { input: { fontFamily: 'var(--mantine-font-family-monospace)' } }

/** Consumer key and secret inputs, shared by connecting a shop and replacing its keys. */
export function KeyFields({
  keys,
  onChange,
  errors,
  autoFocus,
}: {
  keys: Keys
  onChange: (keys: Keys) => void
  errors: Record<string, string>
  autoFocus?: boolean
}) {
  const { t } = useTranslation()
  return (
    <>
      <TextInput
        label={t('channels.form.consumerKey')}
        withAsterisk
        placeholder="ck_…"
        autoComplete="off"
        spellCheck={false}
        data-autofocus={autoFocus || undefined}
        styles={monospace}
        value={keys.consumerKey}
        onChange={(e) => onChange({ ...keys, consumerKey: e.currentTarget.value.trim() })}
        error={errors.consumerKey}
      />
      <PasswordInput
        label={t('channels.form.consumerSecret')}
        withAsterisk
        placeholder="cs_…"
        autoComplete="off"
        styles={monospace}
        value={keys.consumerSecret}
        onChange={(e) => onChange({ ...keys, consumerSecret: e.currentTarget.value.trim() })}
        error={errors.consumerSecret}
      />
    </>
  )
}
