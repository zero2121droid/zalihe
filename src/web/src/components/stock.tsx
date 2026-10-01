import { Box, Group, Text } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import type { StockStatus, Unit } from '../api/generated/model'
import { formatNumber, formatSignedQuantity } from '../lib/format'

/** Token colors per status (DESIGN.md "Status zaliha"). */
const statusColor: Record<StockStatus, string> = {
  inStock: 'var(--z-status-ok)',
  low: 'var(--z-status-low)',
  outOfStock: 'var(--z-status-out)',
}

/** Status as a dot and a label; never color alone. */
export function StockStatusLabel({ status }: { status: StockStatus }) {
  const { t } = useTranslation()
  return (
    <Group gap={8} wrap="nowrap">
      <Box w={7} h={7} bg={statusColor[status]} style={{ borderRadius: '50%', flexShrink: 0 }} aria-hidden />
      <Text component="span" fz="sm" c={statusColor[status]}>
        {t(`stock.status.${status}`)}
      </Text>
    </Group>
  )
}

/**
 * A stock quantity with its unit: "31 kom". The number is colored for low and out of stock;
 * in stock it stays the text color (DESIGN.md).
 */
export function StockQuantity({ quantity, unit, status, size = 18 }: { quantity: number; unit: Unit; status: StockStatus; size?: number }) {
  const { t, i18n } = useTranslation()
  return (
    <Text
      component="span"
      ff="monospace"
      fz={size}
      fw={500}
      c={status === 'inStock' ? undefined : statusColor[status]}
      style={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}
    >
      {formatNumber(quantity, i18n.language)}{' '}
      <Text component="span" fz={size >= 24 ? 13 : 11} c="dimmed" fw={400}>
        {t(`items.units.${unit}`)}
      </Text>
    </Text>
  )
}

/** A movement quantity with its sign: "+24" in the incoming color, "−2" in the text color. */
export function MovementQuantity({ quantity }: { quantity: number }) {
  const { i18n } = useTranslation()
  return (
    <Text
      component="span"
      ff="monospace"
      fz="sm"
      fw={500}
      c={quantity > 0 ? 'var(--z-incoming)' : undefined}
      style={{ fontVariantNumeric: 'tabular-nums' }}
    >
      {formatSignedQuantity(quantity, i18n.language)}
    </Text>
  )
}
