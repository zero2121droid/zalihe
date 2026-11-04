import { Anchor, Group, Stack, Text, Title } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { LanguageSwitch } from '../components/LanguageSwitch'
import { Logo } from '../components/Logo'
import { ThemeToggle } from '../components/ThemeToggle'
import { formatDate } from '../lib/format'
import { legal } from './legal'

const sections = ['controller', 'data', 'notStored', 'purposes', 'platform', 'processors', 'retention', 'cookies', 'security', 'rights'] as const

/** The privacy policy: public, signed in or not, so it can be read before registering. */
export function PrivacyPage() {
  const { t, i18n } = useTranslation()
  return (
    <Stack maw={720} mx="auto" px="md" py="xl" gap="lg">
      <Group justify="space-between">
        <Anchor component={Link} to="/" aria-label={t('common.appName')}>
          <Logo />
        </Anchor>
        <Group gap="xs">
          <LanguageSwitch />
          <ThemeToggle />
        </Group>
      </Group>

      <Stack gap={4}>
        <Title order={1} lts="-0.01em">
          {t('privacy.title')}
        </Title>
        <Text fz="xs" c="dimmed">
          {t('privacy.updated', { date: formatDate(legal.updated, i18n.language) })}
        </Text>
      </Stack>

      <Text>{t('privacy.intro')}</Text>

      {sections.map((section) => (
        <Stack key={section} component="section" gap={4}>
          <Title order={2}>{t(`privacy.sections.${section}.title`)}</Title>
          <Text style={{ whiteSpace: 'pre-line' }}>
            {t(`privacy.sections.${section}.body`, { controller: legal.controller, email: legal.email })}
          </Text>
        </Stack>
      ))}
    </Stack>
  )
}
