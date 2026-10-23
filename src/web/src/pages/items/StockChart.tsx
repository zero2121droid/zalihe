import { Box, Group, Text, Title } from '@mantine/core'
import { useElementSize } from '@mantine/hooks'
import { keepPreviousData } from '@tanstack/react-query'
import { type PointerEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useGetItemStockHistory } from '../../api/generated/items/items'
import type { ItemDto } from '../../api/generated/model'
import { FilterPill, FilterPills } from '../../components/FilterPill'
import { formatNumber, formatShortDate } from '../../lib/format'
import { belowMinimum, steps, stockAt } from '../../lib/stockChart'
import classes from './StockChart.module.css'

const PERIODS = [30, 90] as const
const HEIGHT = 200
const TOP = 12
const BOTTOM = HEIGHT - 24

/**
 * The item's stock over the last 30 or 90 days as a step line (DESIGN.md "Grafik stanja"), with the
 * minimum as a dashed line and the time spent at or below it shaded. Hovering shows the stock at that
 * moment; the history under it lists the same movements as a table.
 */
export function StockChart({ item }: { item: ItemDto }) {
  const { t, i18n } = useTranslation()
  const [days, setDays] = useState<(typeof PERIODS)[number]>(30)
  const history = useGetItemStockHistory(item.id, { days }, { query: { placeholderData: keepPreviousData } })
  const { ref, width } = useElementSize()
  const [hover, setHover] = useState<number | null>(null)
  const data = history.data
  const unit = t(`items.units.${item.unit}`)
  const number = (value: number) => formatNumber(value, i18n.language)

  const levels = data ? steps(data) : []
  const from = data ? Date.parse(data.from) : 0
  const to = data ? Date.parse(data.to) : 1
  const values = levels.map((l) => l.stock)
  const top = Math.max(...values, item.minStock, 1) * 1.15
  const bottom = Math.min(0, ...values)
  const x = (at: number) => ((at - from) / (to - from)) * width
  const y = (stock: number) => BOTTOM - ((stock - bottom) / (top - bottom)) * (BOTTOM - TOP)
  const path =
    levels.map((l, i) => (i === 0 ? `M0 ${y(l.stock)}` : `H${x(l.at)} V${y(l.stock)}`)).join(' ') + ` H${width}`
  const low = data ? belowMinimum(data, item.minStock) : []

  function onMove(event: PointerEvent<SVGSVGElement>) {
    const box = event.currentTarget.getBoundingClientRect()
    const share = Math.min(1, Math.max(0, (event.clientX - box.left) / box.width))
    setHover(from + share * (to - from))
  }

  return (
    <Box component="section" aria-label={t('stock.chart.title')} bg="var(--z-surface)" bd="1px solid var(--z-line)" style={{ borderRadius: 'var(--mantine-radius-md)' }}>
      <Group justify="space-between" px="lg" py="sm" gap="sm" style={{ borderBottom: '1px solid var(--z-line)' }}>
        <Title order={2}>{t('stock.chart.title')}</Title>
        <FilterPills label={t('stock.chart.period')}>
          {PERIODS.map((period) => (
            <FilterPill key={period} active={days === period} onClick={() => setDays(period)}>
              {t('stock.chart.days', { count: period })}
            </FilterPill>
          ))}
        </FilterPills>
      </Group>

      <div ref={ref} className={classes.plot}>
        {data && width > 0 && (
          <svg
            width={width}
            height={HEIGHT}
            role="img"
            aria-label={t('stock.chart.summary', {
              days,
              start: number(data.startStock),
              end: number(values.at(-1)!),
              lowest: number(Math.min(...values)),
              unit,
            })}
            onPointerMove={onMove}
            onPointerLeave={() => setHover(null)}
          >
            <line x1={0} x2={width} y1={y(0)} y2={y(0)} stroke="var(--z-line)" />
            {low.map((span) => (
              <rect
                key={span.from}
                x={x(span.from)}
                width={Math.max(1, x(span.to) - x(span.from))}
                y={y(item.minStock)}
                height={y(bottom) - y(item.minStock)}
                fill="var(--z-status-low)"
                fillOpacity={0.12}
              />
            ))}
            {item.minStock > 0 && (
              <>
                <line x1={0} x2={width} y1={y(item.minStock)} y2={y(item.minStock)} stroke="var(--z-status-low)" strokeDasharray="4 4" />
                <text x={width} y={y(item.minStock) - 6} textAnchor="end" className={classes.axis}>
                  {t('stock.chart.minimum', { value: number(item.minStock) })}
                </text>
              </>
            )}
            <path key={days} className={classes.line} d={path} fill="none" stroke="var(--z-accent)" strokeWidth={2} strokeLinejoin="round" />
            <circle cx={width} cy={y(values.at(-1)!)} r={4} fill="var(--z-accent)" stroke="var(--z-surface)" strokeWidth={2} />
            {hover !== null && (
              <>
                <line x1={x(hover)} x2={x(hover)} y1={TOP} y2={BOTTOM} stroke="var(--z-line-strong)" />
                <circle cx={x(hover)} cy={y(stockAt(data, hover))} r={4} fill="var(--z-accent)" stroke="var(--z-surface)" strokeWidth={2} />
              </>
            )}
            <text x={0} y={HEIGHT - 4} className={classes.axis}>
              {formatShortDate(data.from, i18n.language)}
            </text>
            <text x={width / 2} y={HEIGHT - 4} textAnchor="middle" className={classes.axis}>
              {formatShortDate(new Date((from + to) / 2), i18n.language)}
            </text>
            <text x={width} y={HEIGHT - 4} textAnchor="end" className={classes.axis}>
              {t('stock.chart.today')}
            </text>
          </svg>
        )}
        {data && hover !== null && (
          <div className={classes.tip} style={{ left: Math.min(Math.max(x(hover), 60), width - 60) }} aria-hidden>
            <Text fz="xs" c="dimmed">
              {formatShortDate(new Date(hover), i18n.language)}
            </Text>
            <Text ff="monospace" fz="sm" fw={500}>
              {number(stockAt(data, hover))}{' '}
              <Text component="span" fz={11} c="dimmed">
                {unit}
              </Text>
            </Text>
          </div>
        )}
      </div>

      {low.length > 0 && (
        <Text fz="xs" c="dimmed" px="lg" pb="md">
          <span className={classes.swatch} aria-hidden />
          {t('stock.chart.legendBelow')}
        </Text>
      )}
    </Box>
  )
}
