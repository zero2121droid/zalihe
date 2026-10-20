import { Group, Text } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import type { SalesChannelStatus } from '../../api/generated/model'
import classes from './ChannelStatus.module.css'

const statusColor: Record<SalesChannelStatus | 'checking', string> = {
  checking: 'var(--z-accent-text)',
  connected: 'var(--z-status-ok)',
  error: 'var(--z-status-out)',
}

/**
 * The shop's connection, always as an icon with text (K from the animation sheet): arrows turn
 * while it's being checked, a check draws itself when a check just succeeded (`fresh`), and while
 * the connection is broken the warning sign nudges now and then.
 */
export function ChannelStatus({ status, checking, fresh }: { status: SalesChannelStatus; checking: boolean; fresh: boolean }) {
  const { t } = useTranslation()
  const shown = checking ? 'checking' : status
  return (
    <Group gap={8} wrap="nowrap" c={statusColor[shown]} role="status">
      <svg className={classes.icon} data-state={shown} data-fresh={fresh || undefined} viewBox="0 0 16 16" aria-hidden>
        {shown === 'checking' && (
          <g className={classes.spin}>
            <path d="M13.2 6A5.5 5.5 0 0 0 3.1 5.6M2.8 10A5.5 5.5 0 0 0 12.9 10.4" />
            <polyline points="13.4,2.6 13.4,6 10,6" />
            <polyline points="2.6,13.4 2.6,10 6,10" />
          </g>
        )}
        {shown === 'connected' && <polyline className={classes.check} points="3,8.5 6.5,12 13,4.5" />}
        {shown === 'error' && (
          <g className={classes.nudge}>
            <path d="M8 2.2 14.4 13.4H1.6Z" />
            <line x1="8" y1="6.6" x2="8" y2="9.4" />
            <line x1="8" y1="11.6" x2="8" y2="11.7" />
          </g>
        )}
      </svg>
      <Text component="span" fw={500} c="inherit">
        {t(`channels.status.${shown}`)}
      </Text>
    </Group>
  )
}
