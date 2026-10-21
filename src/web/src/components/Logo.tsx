import { Group, Text } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { MARK_BODY, MARK_LID } from './logoMark'

export function LogoMark({ size = 16 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="var(--z-accent-text)" aria-hidden>
      <path d={MARK_LID} />
      <path d={MARK_BODY} fillRule="evenodd" />
    </svg>
  )
}

export function Logo() {
  const { t } = useTranslation()
  return (
    <Group gap={10} wrap="nowrap">
      <LogoMark />
      <Text ff="monospace" fz={17} fw={600} lts="-0.02em">
        {t('common.appName')}
      </Text>
    </Group>
  )
}
