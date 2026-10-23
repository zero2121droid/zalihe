import { Group, Text } from '@mantine/core'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useGetItemShopStock } from '../api/generated/items/items'
import classes from '../pages/channels/ChannelStatus.module.css'

const PENDING_POLL_MS = 5000

/**
 * Whether the shops the item is linked to show its stock yet (a change is sent about 30 s after it
 * is made). Arrows turn while a change is on its way; when the shop has it, a check draws itself.
 * Nothing shows for an item that isn't in any shop. Only what the API reports, never a guessed state.
 */
export function ShopStockStatus({ itemId }: { itemId: string }) {
  const { t } = useTranslation()
  const status = useGetItemShopStock(itemId, {
    query: { refetchInterval: (query) => (query.state.data?.pending ? PENDING_POLL_MS : false) },
  })
  const pending = status.data?.pending
  const fresh = useJustSent(pending)

  if (!status.data?.shops) return null
  const count = status.data.shops

  return (
    <Group gap={6} wrap="nowrap" c={pending ? 'var(--z-accent-text)' : 'dimmed'} role="status">
      <svg className={classes.icon} data-fresh={fresh || undefined} viewBox="0 0 16 16" aria-hidden>
        {pending ? (
          <g className={classes.spin}>
            <path d="M13.2 6A5.5 5.5 0 0 0 3.1 5.6M2.8 10A5.5 5.5 0 0 0 12.9 10.4" />
            <polyline points="13.4,2.6 13.4,6 10,6" />
            <polyline points="2.6,13.4 2.6,10 6,10" />
          </g>
        ) : (
          <polyline className={classes.check} points="3,8.5 6.5,12 13,4.5" />
        )}
      </svg>
      <Text component="span" fz="xs" c="inherit">
        {t(pending ? 'stock.shop.pending' : 'stock.shop.synced', { count })}
      </Text>
    </Group>
  )
}

/** True once pending turns into sent, so the check draws itself only right after a send. */
function useJustSent(pending: boolean | undefined) {
  const [seen, setSeen] = useState(pending)
  const [fresh, setFresh] = useState(false)

  if (seen !== pending) {
    setSeen(pending)
    if (seen === true && pending === false) setFresh(true)
  }

  return fresh
}
