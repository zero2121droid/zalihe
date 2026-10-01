import { SegmentedControl } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { isLanguage, languages, setLanguage } from '../i18n'

/** Language choice before sign-in. Signed-in users change it in settings (saved on the user). */
export function LanguageSwitch() {
  const { t, i18n } = useTranslation()
  return (
    <SegmentedControl
      size="xs"
      aria-label={t('common.language')}
      value={i18n.language}
      onChange={(value) => isLanguage(value) && void setLanguage(value)}
      data={languages.map((language) => ({ value: language, label: t(`common.languages.${language}`) }))}
    />
  )
}
