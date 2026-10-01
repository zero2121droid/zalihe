import { Anchor, Box, Button, Center, Group, Loader, SimpleGrid, Stack, Text, Title } from '@mantine/core'
import { type ReactNode, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { formErrorMessage } from '../api/errors'
import { useGetDashboard } from '../api/generated/dashboard/dashboard'
import type { DashboardDto, RecentMovementDto, ReorderItemDto } from '../api/generated/model'
import { Eyebrow } from '../components/Eyebrow'
import { MovementQuantity, StockQuantity } from '../components/stock'
import { formatLongDate, formatMoney, formatNumber, formatRecentTime } from '../lib/format'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

export function HomePage() {
  const { t, i18n } = useTranslation()
  const [today] = useState(() => new Date())
  const dashboard = useGetDashboard()

  return (
    <Stack gap={28}>
      <Stack gap={4}>
        <Eyebrow>{formatLongDate(today, i18n.language)}</Eyebrow>
        <Title order={1} lts="-0.01em">
          {t('home.title')}
        </Title>
      </Stack>

      {dashboard.isPending ? (
        <Center py="xl">
          <Loader size="sm" aria-label={t('common.loading')} />
        </Center>
      ) : dashboard.isError ? (
        <Text role="alert">{formErrorMessage(t, dashboard.error)}</Text>
      ) : dashboard.data.activeItems === 0 && dashboard.data.recentMovements.length === 0 ? (
        <EmptyHome />
      ) : (
        <Overview data={dashboard.data} />
      )}
    </Stack>
  )
}

function Overview({ data }: { data: DashboardDto }) {
  const { t, i18n } = useTranslation()
  const number = (value: number) => formatNumber(value, i18n.language)

  return (
    <>
      {/* One strip divided by lines, not separate cards (DESIGN.md). */}
      <SimpleGrid
        component="section"
        aria-label={t('home.title')}
        cols={{ base: 2, md: 4 }}
        spacing={1}
        verticalSpacing={1}
        bg="var(--z-line)"
        bd="1px solid var(--z-line)"
        style={{ ...sectionStyle, overflow: 'hidden' }}
      >
        <Figure label={t('home.figures.stockValue')} hint={t('home.figures.stockValueHint')}>
          {formatMoney(data.stockValue, i18n.language)}{' '}
          <Text component="span" fz={13} c="dimmed" fw={400}>
            {t('common.currency')}
          </Text>
        </Figure>
        <Figure label={t('home.figures.activeItems')} hint={t('home.figures.activeItemsHint')}>
          {number(data.activeItems)}
        </Figure>
        <Figure
          label={t('home.figures.belowMinimum')}
          hint={t('home.figures.belowMinimumHint')}
          color={data.belowMinimum > 0 ? 'var(--z-status-low)' : undefined}
        >
          {number(data.belowMinimum)}
        </Figure>
        <Figure
          label={t('home.figures.outOfStock')}
          hint={t('home.figures.outOfStockHint')}
          color={data.outOfStock > 0 ? 'var(--z-status-out)' : undefined}
        >
          {number(data.outOfStock)}
        </Figure>
      </SimpleGrid>

      <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="lg" style={{ alignItems: 'start' }}>
        <Panel title={t('home.reorder.title')} link={{ to: '/items?status=low', label: t('home.reorder.all') }}>
          {data.reorder.length === 0 ? (
            <Text p="lg" c="dimmed">
              {t('home.reorder.none')}
            </Text>
          ) : (
            data.reorder.map((item) => <ReorderRow key={item.itemId} item={item} />)
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

function ReorderRow({ item }: { item: ReorderItemDto }) {
  const { t, i18n } = useTranslation()
  // How much of the minimum is left, for the bar (out of stock shows an empty bar).
  const share = item.minStock > 0 ? Math.min(Math.max(item.stock / item.minStock, 0), 1) : 0
  const color = item.status === 'outOfStock' ? 'var(--z-status-out)' : 'var(--z-status-low)'

  return (
    <Group px="lg" py={13} gap="md" wrap="nowrap" style={rowStyle}>
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
      <Text ff="monospace" fz="xs" c="dimmed" w={48} pt={2} style={{ flexShrink: 0 }}>
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

function EmptyHome() {
  const { t } = useTranslation()
  return (
    <Box component="section" p="lg" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Stack gap="sm" align="flex-start">
        <Stack gap={4}>
          <Title order={2}>{t('home.empty.title')}</Title>
          <Text c="dimmed">{t('home.empty.description')}</Text>
          <Text c="dimmed">{t('home.empty.actions')}</Text>
        </Stack>
        <Group gap="sm">
          <Button component={Link} to="/items">
            {t('items.new')}
          </Button>
          <Button component={Link} to="/items/import" variant="default">
            {t('items.importCsv')}
          </Button>
        </Group>
      </Stack>
    </Box>
  )
}
