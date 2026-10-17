import { Box, Button, Group, Stack, Text } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { Link, useLocation } from 'react-router'
import { useListChannels } from '../api/generated/channels/channels'
import { formatDateTime } from '../lib/format'

/** How often open pages ask again whether a shop stopped working (SPEC 11.1: polling in v1). */
const REFRESH_MS = 5 * 60_000

/**
 * A shop the application can't reach (SPEC 7.6): shown above every page, because while it lasts
 * stock isn't sent to the shop and it may sell what is gone. The Channels page shows it on the card instead.
 */
export function ChannelAlert() {
  const { t, i18n } = useTranslation()
  const { pathname } = useLocation()
  const channels = useListChannels({ query: { refetchInterval: REFRESH_MS } })
  const failing = channels.data?.filter((c) => c.status === 'error') ?? []
  if (failing.length === 0 || pathname === '/channels') return null

  return (
    <Stack gap="sm" mb="lg">
      {failing.map((channel) => (
        <Box
          key={channel.id}
          role="alert"
          p="md"
          bg="var(--z-surface)"
          bd="1px solid var(--z-line-strong)"
          style={{ borderRadius: 'var(--mantine-radius-md)' }}
        >
          <Group justify="space-between" align="center" gap="md">
            <Stack gap={4} flex="1 1 320px">
              <Group gap={8} wrap="nowrap">
                <Box w={8} h={8} bg="var(--z-status-out)" style={{ borderRadius: '50%', flexShrink: 0 }} aria-hidden />
                <Text fw={500} c="var(--z-status-out)">
                  {channel.failingSince
                    ? t('channels.alert.titleSince', {
                        shop: new URL(channel.baseUrl).host,
                        date: formatDateTime(channel.failingSince, i18n.language),
                      })
                    : t('channels.alert.title', { shop: new URL(channel.baseUrl).host })}
                </Text>
              </Group>
              <Text fz="sm" c="var(--z-text-2)">
                {channel.lastErrorCode &&
                  t(`errors.${channel.lastErrorCode}`, { defaultValue: t('errors.common.unexpected') }) + ' '}
                {t('channels.alert.impact')}
              </Text>
            </Stack>
            <Button variant="default" component={Link} to="/channels">
              {t('channels.alert.open')}
            </Button>
          </Group>
        </Box>
      ))}
    </Stack>
  )
}
