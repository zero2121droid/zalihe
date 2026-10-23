import { Box, Group, Text } from '@mantine/core'
import { useReducedMotion } from '@mantine/hooks'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { StockStatus, Unit } from '../api/generated/model'
import { formatNumber, formatSignedQuantity } from '../lib/format'
import { useAnimatedNumber } from '../lib/useAnimatedNumber'
import classes from './stock.module.css'

/** Token colors per status (DESIGN.md "Status zaliha"). */
const statusColor: Record<StockStatus, string> = {
  inStock: 'var(--z-status-ok)',
  low: 'var(--z-status-low)',
  outOfStock: 'var(--z-status-out)',
}

/**
 * Status as a dot and a label; never color alone. With `pulse`, the dot of a low or empty stock
 * sends out a ring now and then, for places that show one item and should draw the eye. With
 * `animate`, a new status (stock crossed the minimum or ran out) pops the dot and slides the label in.
 */
export function StockStatusLabel({
  status,
  pulse = false,
  animate = false,
}: {
  status: StockStatus
  pulse?: boolean
  animate?: boolean
}) {
  const { t } = useTranslation()
  const changed = useStatusChange(status, animate)
  const dotClasses = [classes.dot, pulse && status !== 'inStock' && classes.pulse, changed && classes.dotChanged]
  return (
    <Group gap={8} wrap="nowrap" key={changed}>
      <Box
        w={7}
        h={7}
        bg={statusColor[status]}
        c={statusColor[status]}
        className={dotClasses.filter(Boolean).join(' ')}
        style={{ borderRadius: '50%', flexShrink: 0 }}
        aria-hidden
      />
      <Text component="span" fz="sm" c={statusColor[status]} className={changed ? classes.labelChanged : undefined}>
        {t(`stock.status.${status}`)}
      </Text>
    </Group>
  )
}

/** A number that grows each time the status changes (0 until it does), to replay the change. */
function useStatusChange(status: StockStatus, enabled: boolean) {
  const reduceMotion = useReducedMotion()
  const [seen, setSeen] = useState(status)
  const [changes, setChanges] = useState(0)

  if (seen !== status) {
    setSeen(status)
    if (enabled && !reduceMotion) setChanges(changes + 1)
  }

  return changes
}

interface StockChange {
  id: number
  delta: number
}

/** The last change of a shown quantity, so it can float over the number; null when there is none. */
function useStockChange(quantity: number, enabled: boolean) {
  const reduceMotion = useReducedMotion()
  const previous = useRef(quantity)
  const [change, setChange] = useState<StockChange | null>(null)

  useEffect(() => {
    const delta = Math.round((quantity - previous.current) * 1000) / 1000
    previous.current = quantity
    if (enabled && !reduceMotion && delta !== 0) setChange((last) => ({ id: (last?.id ?? 0) + 1, delta }))
  }, [quantity, enabled, reduceMotion])

  return [change, () => setChange(null)] as const
}

interface StockQuantityProps {
  quantity: number
  unit: Unit
  status: StockStatus
  size?: number
  /** Glide to a new value and show the change ("+24") when the quantity changes. */
  animate?: boolean
}

/**
 * A stock quantity with its unit: "31 kom". The number is colored for low and out of stock;
 * in stock it stays the text color (DESIGN.md).
 */
export function StockQuantity({ quantity, unit, status, size = 18, animate = false }: StockQuantityProps) {
  const { t, i18n } = useTranslation()
  const animated = useAnimatedNumber(quantity)
  const [change, clearChange] = useStockChange(quantity, animate)
  const shown = animate ? animated : quantity

  return (
    <Text
      component="span"
      ff="monospace"
      fz={size}
      fw={500}
      c={status === 'inStock' ? undefined : statusColor[status]}
      className={animate ? classes.quantity : undefined}
      style={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}
    >
      {change && (
        <span key={change.id} aria-hidden>
          <span className={`${classes.flash} ${change.delta > 0 ? classes.incoming : classes.outgoing}`} />
          <span
            className={`${classes.delta} ${change.delta > 0 ? classes.incoming : classes.outgoing}`}
            onAnimationEnd={clearChange}
          >
            {formatSignedQuantity(change.delta, i18n.language)}
          </span>
        </span>
      )}
      {formatNumber(shown, i18n.language)}{' '}
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
