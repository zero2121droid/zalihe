import { ActionIcon, useComputedColorScheme, useMantineColorScheme } from '@mantine/core'
import { IconMoon, IconSun } from '@tabler/icons-react'
import { useTranslation } from 'react-i18next'

/** Switches between the dark and light theme. The label says what a click will do. */
export function ThemeToggle() {
  const { t } = useTranslation()
  const { setColorScheme } = useMantineColorScheme()
  const scheme = useComputedColorScheme('dark', { getInitialValueInEffect: false })
  const label = scheme === 'dark' ? t('common.theme.toLight') : t('common.theme.toDark')

  return (
    <ActionIcon
      size={36}
      aria-label={label}
      title={label}
      onClick={() => setColorScheme(scheme === 'dark' ? 'light' : 'dark')}
    >
      {scheme === 'dark' ? <IconSun size={16} stroke={1.8} /> : <IconMoon size={16} stroke={1.8} />}
    </ActionIcon>
  )
}
