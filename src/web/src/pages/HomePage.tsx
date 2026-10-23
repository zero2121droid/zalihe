import { Anchor, Box, Button, Group, SimpleGrid, Stack, Text, Title } from '@mantine/core'
import { IconCheck } from '@tabler/icons-react'
import { type CSSProperties, type ReactNode, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { formErrorMessage } from '../api/errors'
import { useGetDashboard } from '../api/generated/dashboard/dashboard'
import type { DashboardDto, RecentMovementDto, ReorderItemDto, SetupDto } from '../api/generated/model'
import { Eyebrow } from '../components/Eyebrow'
import { PageLoader } from '../components/loaders'
import feedbackClasses from '../components/feedback.module.css'
import { MovementQuantity, StockQuantity } from '../components/stock'
import { useAnimatedNumber } from '../lib/useAnimatedNumber'
import { useLoadingDisplay } from '../lib/useLoadingDisplay'
import { formatLongDate, formatMoney, formatNumber, formatRecentTime } from '../lib/format'
import classes from './HomePage.module.css'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

export function HomePage() {
  const { t, i18n } = useTranslation()
  const [today] = useState(() => new Date())
  const dashboard = useGetDashboard()
  const display = useLoadingDisplay(dashboard.isPending)

  return (
    <Stack gap={28}>
      <Stack gap={4}>
        <Eyebrow>{formatLongDate(today, i18n.language)}</Eyebrow>
        <Title order={1} lts="-0.01em">
          {t('home.title')}
        </Title>
      </Stack>

      {dashboard.isPending || display !== 'content' ? (
        display === 'loader' && <PageLoader />
      ) : dashboard.isError ? (
        <Text role="alert">{formErrorMessage(t, dashboard.error)}</Text>
      ) : (
        <>
          {dashboard.data.ordersWithUnlinked > 0 && <UnlinkedOrders count={dashboard.data.ordersWithUnlinked} />}
          {!(dashboard.data.setup.hasItems && dashboard.data.setup.hasMinStock) && <FirstSteps setup={dashboard.data.setup} />}
          {(dashboard.data.activeItems > 0 || dashboard.data.recentMovements.length > 0) && <Overview data={dashboard.data} />}
        </>
      )}
    </Stack>
  )
}

function Overview({ data }: { data: DashboardDto }) {
  const { t, i18n } = useTranslation()
  const number = (value: number) => formatNumber(value, i18n.language)
  // The figures count up from 0 when the screen opens.
  const stockValue = useAnimatedNumber(data.stockValue, 0)
  const activeItems = useAnimatedNumber(data.activeItems, 0)
  const belowMinimum = useAnimatedNumber(data.belowMinimum, 0)
  const outOfStock = useAnimatedNumber(data.outOfStock, 0)

  return (
    <>
      {/* One strip divided by lines, not separate cards (DESIGN.md). */}
      <SimpleGrid
        component="section"
        aria-label={t('home.title')}
        className="z-kpi"
        cols={{ base: 2, md: 4 }}
        spacing={1}
        verticalSpacing={1}
        bg="var(--z-line)"
        bd="1px solid var(--z-line)"
        style={{ ...sectionStyle, overflow: 'hidden' }}
      >
        <Figure label={t('home.figures.stockValue')} hint={t('home.figures.stockValueHint')}>
          {formatMoney(stockValue, i18n.language)}{' '}
          <Text component="span" fz={13} c="dimmed" fw={400}>
            {t('common.currency')}
          </Text>
        </Figure>
        <Figure label={t('home.figures.activeItems')} hint={t('home.figures.activeItemsHint')}>
          {number(activeItems)}
        </Figure>
        <Figure
          label={t('home.figures.belowMinimum')}
          hint={t('home.figures.belowMinimumHint')}
          color={data.belowMinimum > 0 ? 'var(--z-status-low)' : undefined}
        >
          {number(belowMinimum)}
        </Figure>
        <Figure
          label={t('home.figures.outOfStock')}
          hint={t('home.figures.outOfStockHint')}
          color={data.outOfStock > 0 ? 'var(--z-status-out)' : undefined}
        >
          {number(outOfStock)}
        </Figure>
      </SimpleGrid>

      <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="lg" style={{ alignItems: 'start' }}>
        <Panel title={t('home.reorder.title')} link={{ to: '/items?status=low', label: t('home.reorder.all') }}>
          {data.reorder.length === 0 ? (
            <Text p="lg" c="dimmed">
              {t('home.reorder.none')}
            </Text>
          ) : (
            data.reorder.map((item, index) => <ReorderRow key={item.itemId} item={item} index={index} />)
          )}
        </Panel>
        <Panel title={t('home.recent.title')} link={{ to: '/items', label: t('home.recent.all') }}>
          {data.recentMovements.length === 0 ? (
            <Text p="lg" c="dimmed">
              {t('home.recent.none')}
            </Text>
          ) : (
            data.recentMovements.map((movement) => <RecentRow key={movement.id} movement={movement} />)
          )}
        </Panel>
      </SimpleGrid>
    </>
  )
}

function Figure({ label, hint, color, children }: { label: string; hint: string; color?: string; children: ReactNode }) {
  return (
    <Stack gap={6} p="md" px="lg" bg="var(--z-surface)">
      <Text fz="xs" c="dimmed">
        {label}
      </Text>
      <Text ff="monospace" fz={26} fw={500} c={color} lts="-0.02em" style={{ fontVariantNumeric: 'tabular-nums' }}>
        {children}
      </Text>
      <Text fz="xs" c="dimmed">
        {hint}
      </Text>
    </Stack>
  )
}

function Panel({ title, link, children }: { title: string; link: { to: string; label: string }; children: ReactNode }) {
  return (
    <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Group justify="space-between" px="lg" py="md" style={{ borderBottom: '1px solid var(--z-line)' }}>
        <Title order={2}>{title}</Title>
        <Anchor component={Link} to={link.to} fz="sm">
          {link.label}
        </Anchor>
      </Group>
      {children}
    </Box>
  )
}

const rowStyle = { borderBottom: '1px solid var(--z-line-soft)' }

/** `index` staggers the rows: they appear one after another when the screen opens. */
function ReorderRow({ item, index }: { item: ReorderItemDto; index: number }) {
  const { t, i18n } = useTranslation()
  // How much of the minimum is left, for the bar (out of stock shows an empty bar).
  const share = item.minStock > 0 ? Math.min(Math.max(item.stock / item.minStock, 0), 1) : 0
  const color = item.status === 'outOfStock' ? 'var(--z-status-out)' : 'var(--z-status-low)'

  return (
    <Group
      px="lg"
      py={13}
      gap="md"
      wrap="nowrap"
      className={feedbackClasses.enter}
      style={{ ...rowStyle, '--i': index } as CSSProperties}
    >
      <Stack gap={2} miw={0} flex={1}>
        <Anchor component={Link} to={`/items/${item.itemId}`} fw={500} c="var(--z-text)" truncate>
          {item.name}
        </Anchor>
        <Text ff="monospace" fz={11} c="dimmed">
          {item.sku}
        </Text>
      </Stack>
      <Stack gap={6} w={132} visibleFrom="xs">
        <Box h={4} bg="var(--z-line)" style={{ borderRadius: 2 }}>
          <Box h={4} w={`${share * 100}%`} bg={color} style={{ borderRadius: 2 }} />
        </Box>
        <Text fz={11} c="dimmed">
          {t('home.reorder.min', { value: formatNumber(item.minStock, i18n.language), unit: t(`items.units.${item.unit}`) })}
        </Text>
      </Stack>
      <Box w={88} ta="right">
        <StockQuantity quantity={item.stock} unit={item.unit} status={item.status} size={20} />
      </Box>
    </Group>
  )
}

function RecentRow({ movement }: { movement: RecentMovementDto }) {
  const { t, i18n } = useTranslation()
  const detail = movement.externalRef ? `${t(`stock.sources.${movement.source}`)} #${movement.externalRef}` : (movement.note ?? t(`stock.sources.${movement.source}`))

  return (
    <Group px="lg" py={12} gap="sm" wrap="nowrap" align="flex-start" style={rowStyle}>
      <Text ff="monospace" fz="xs" c="dimmed" w={56} pt={2} style={{ flexShrink: 0, whiteSpace: 'nowrap' }}>
        {formatRecentTime(movement.occurredAt, i18n.language, t('home.recent.yesterday'))}
      </Text>
      <Stack gap={2} miw={0} flex={1}>
        <Anchor component={Link} to={`/items/${movement.itemId}`} c="var(--z-text)" truncate>
          {movement.itemName}
        </Anchor>
        <Text fz="xs" c="dimmed" truncate>
          {t(`stock.types.${movement.type}`)} · {detail}
        </Text>
      </Stack>
      <MovementQuantity quantity={movement.quantity} />
    </Group>
  )
}

/**
 * Orders whose products took no stock because they aren't linked (DESIGN.md "Početna"): the stock is
 * off until they are, so it stands above the figures, like a shop that stopped working.
 */
function UnlinkedOrders({ count }: { count: number }) {
  const { t } = useTranslation()
  return (
    <Box role="alert" p="md" bg="var(--z-surface)" bd="1px solid var(--z-line-strong)" style={sectionStyle}>
      <Group justify="space-between" align="center" gap="md">
        <Stack gap={4} flex="1 1 320px">
          <Group gap={8} wrap="nowrap">
            <Box w={8} h={8} bg="var(--z-status-low)" style={{ borderRadius: '50%', flexShrink: 0 }} aria-hidden />
            <Text fw={500} c="var(--z-status-low)">
              {t('home.unlinked.title', { count })}
            </Text>
          </Group>
          <Text fz="sm" c="var(--z-text-2)">
            {t('home.unlinked.impact')}
          </Text>
        </Stack>
        <Button variant="default" component={Link} to="/orders?unlinked=1">
          {t('home.unlinked.open')}
        </Button>
      </Group>
    </Box>
  )
}

/**
 * The first steps of a new company (DESIGN.md "Prvi koraci"): items, then minimum stock, each checked
 * off when it is really done. Connecting a shop is offered too, but it is optional: a company without
 * a web shop works by hand or from CSV, so the checklist goes away once the first two are done.
 */
function FirstSteps({ setup }: { setup: SetupDto }) {
  const { t } = useTranslation()
  const done = [setup.hasItems, setup.hasMinStock].filter(Boolean).length
  const total = 2

  return (
    <Box component="section" aria-label={t('home.setup.eyebrow')} bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Group justify="space-between" align="flex-end" gap="md" px="lg" py="md">
        <Stack gap={4}>
          <Eyebrow>{t('home.setup.eyebrow')}</Eyebrow>
          <Title order={2}>{t('home.setup.title')}</Title>
          <Text fz="xs" c="dimmed">
            {t('home.setup.description')}
          </Text>
        </Stack>
        <Stack gap={6} align="flex-end">
          <Text ff="monospace" fz="xs" c="var(--z-text-2)">
            {t('home.setup.progress', { done, total })}
          </Text>
          <div className={classes.bar} aria-hidden>
            <div className={classes.fill} style={{ width: `${(done / total) * 100}%` }} />
          </div>
        </Stack>
      </Group>

      <Step number={1} done={setup.hasItems} title={t('home.setup.items.title')} text={t(setup.hasItems ? 'home.setup.items.done' : 'home.setup.items.description')}>
        <Group gap="xs" className="z-phone-stack">
          <Button component={Link} to="/items?new=1">
            {t('items.new')}
          </Button>
          <Button component={Link} to="/items/import" variant="default">
            {t('items.importCsv')}
          </Button>
        </Group>
      </Step>
      <Step
        number={2}
        done={setup.hasMinStock}
        title={t('home.setup.minStock.title')}
        text={t(setup.hasMinStock ? 'home.setup.minStock.done' : 'home.setup.minStock.description')}
      >
        <Button component={Link} to="/items" variant={setup.hasItems ? 'filled' : 'default'}>
          {t('home.setup.minStock.action')}
        </Button>
      </Step>
      <Step
        number={3}
        done={setup.hasShop}
        title={t('home.setup.shop.title')}
        optional={t('home.setup.shop.optional')}
        text={t(setup.hasShop ? 'home.setup.shop.done' : 'home.setup.shop.description')}
      >
        <Button component={Link} to="/channels" variant="default">
          {t('home.setup.shop.action')}
        </Button>
      </Step>
    </Box>
  )
}

function Step({
  number,
  done,
  title,
  text,
  optional,
  children,
}: {
  number: number
  done: boolean
  title: string
  text: string
  optional?: string
  children: ReactNode
}) {
  return (
    <div className={classes.step}>
      <span className={classes.mark} data-done={done || undefined} aria-hidden>
        {done ? <IconCheck size={14} stroke={2.2} /> : number}
      </span>
      <div>
        <Group gap="xs">
          <Text fw={500} c={done ? 'var(--z-text-2)' : undefined} td={done ? 'line-through' : undefined}>
            {title}
          </Text>
          {optional && (
            <Text component="span" fz="xs" c="dimmed">
              {optional}
            </Text>
          )}
        </Group>
        <Text fz="xs" c="dimmed">
          {text}
        </Text>
      </div>
      {!done && children}
    </div>
  )
}
