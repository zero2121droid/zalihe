import { ActionIcon, useComputedColorScheme, useMantineColorScheme } from '@mantine/core'
import { IconMoon, IconSun } from '@tabler/icons-react'
import { useTranslation } from 'react-i18next'
import classes from './ThemeToggle.module.css'

/** Switches between the dark and light theme. The label says what a click will do; the icon shows it (sun in dark). */
export function ThemeToggle() {
  const { t } = useTranslation()
  const { setColorScheme } = useMantineColorScheme()
  const scheme = useComputedColorScheme('dark', { getInitialValueInEffect: false })
  const label = scheme === 'dark' ? t('common.theme.toLight') : t('common.theme.toDark')

  return (
    <ActionIcon
      aria-label={label}
      title={label}
      onClick={() => setColorScheme(scheme === 'dark' ? 'light' : 'dark')}
    >
      <span className={classes.icons} aria-hidden>
        <IconSun size={16} stroke={1.8} className={`${classes.icon} ${scheme === 'dark' ? '' : classes.hidden}`} />
        <IconMoon size={16} stroke={1.8} className={`${classes.icon} ${scheme === 'dark' ? classes.hidden : ''}`} />
      </span>
    </ActionIcon>
  )
}
