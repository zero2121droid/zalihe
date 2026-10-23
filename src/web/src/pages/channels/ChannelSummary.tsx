import { Anchor, SimpleGrid, Stack, Text } from '@mantine/core'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { useGetChannelSummary } from '../../api/generated/channels/channels'
import { formatNumber, formatRecentTime } from '../../lib/format'

/**
 * How the shop is doing at a glance (DESIGN.md "Kartica prodavnice"): linked products, orders with
 * products still to link (with the way to them), the last order that arrived and stock waiting to be
 * sent. One strip divided by lines, like the figures on the home screen.
 */
export function ChannelSummary({ channelId }: { channelId: string }) {
  const { t, i18n } = useTranslation()
  const summary = useGetChannelSummary(channelId)
  if (!summary.data) return null
  const data = summary.data
  const number = (value: number) => formatNumber(value, i18n.language)

  return (
    <SimpleGrid
      component="section"
      aria-label={t('channels.summary.label')}
      className="z-kpi"
      cols={{ base: 1, sm: 2, lg: 4 }}
      spacing={1}
      verticalSpacing={1}
      bg="var(--z-line)"
      bd="1px solid var(--z-line)"
      style={{ borderRadius: 'var(--mantine-radius-md)', overflow: 'hidden' }}
    >
      <Figure label={t('channels.summary.linked')}>
        <Text ff="monospace" fz={26} fw={500}>
          {number(data.linkedProducts)}
        </Text>
      </Figure>
      <Figure label={t('channels.summary.unlinked')}>
        <Text ff="monospace" fz={26} fw={500} c={data.ordersWithUnlinked > 0 ? 'var(--z-status-low)' : undefined}>
          {number(data.ordersWithUnlinked)}
        </Text>
        {data.ordersWithUnlinked > 0 && (
          <Anchor component={Link} to="/orders?unlinked=1" fz="xs">
            {t('channels.summary.showOrders')}
          </Anchor>
        )}
      </Figure>
      <Figure label={t('channels.summary.lastOrder')}>
        {data.lastOrderAt ? (
          <>
            <Text ff="monospace" fz={18} fw={500}>
              {formatRecentTime(data.lastOrderAt, i18n.language, t('home.recent.yesterday'))}
            </Text>
            <Text ff="monospace" fz="xs" c="dimmed">
              {t('channels.summary.orderNumber', { number: data.lastOrderNumber })}
            </Text>
          </>
        ) : (
          <Text fz="sm" c="dimmed">
            {t('channels.summary.noOrders')}
          </Text>
        )}
      </Figure>
      <Figure label={t('channels.summary.stock')}>
        <Text fz="sm" c={data.pendingStock > 0 ? 'var(--z-accent-text)' : 'dimmed'}>
          {data.pendingStock > 0 ? t('channels.summary.pending', { count: data.pendingStock }) : t('channels.summary.pendingNone')}
        </Text>
      </Figure>
    </SimpleGrid>
  )
}

function Figure({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Stack gap={4} p="md" px="lg" bg="var(--z-surface)">
      <Text fz="xs" c="dimmed">
        {label}
      </Text>
      {children}
    </Stack>
  )
}
