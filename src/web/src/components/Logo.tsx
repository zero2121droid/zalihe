import { Box, Group, Text } from '@mantine/core'
import { useTranslation } from 'react-i18next'

export function Logo() {
  const { t } = useTranslation()
  return (
    <Group gap={10} wrap="nowrap">
      <Box w={14} h={14} bg="var(--z-accent)" aria-hidden />
      <Text ff="monospace" fz={17} fw={600} lts="-0.02em">
        {t('common.appName')}
      </Text>
    </Group>
  )
}
