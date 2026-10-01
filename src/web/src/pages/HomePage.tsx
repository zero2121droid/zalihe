import { Box, Stack, Text, Title } from '@mantine/core'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Eyebrow } from '../components/Eyebrow'
import { formatLongDate } from '../lib/format'

export function HomePage() {
  const { t, i18n } = useTranslation()
  const [today] = useState(() => new Date())

  return (
    <Stack gap={28}>
      <Stack gap={4}>
        <Eyebrow>{formatLongDate(today, i18n.language)}</Eyebrow>
        <Title order={1} lts="-0.01em">
          {t('home.title')}
        </Title>
      </Stack>

      <Box
        component="section"
        p="lg"
        bg="var(--z-surface)"
        bd="1px solid var(--z-line)"
        style={{ borderRadius: 'var(--mantine-radius-md)' }}
      >
        <Stack gap={4}>
          <Title order={2}>{t('home.empty.title')}</Title>
          <Text c="dimmed">{t('home.empty.description')}</Text>
        </Stack>
      </Box>
    </Stack>
  )
}
