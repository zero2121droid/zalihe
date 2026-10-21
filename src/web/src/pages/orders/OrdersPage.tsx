import { Anchor, Box, Button, Group, Loader, Select, Stack, Table, Text, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import type { OrderDto } from '../../api/generated/model'
import { useListOrders } from '../../api/generated/orders/orders'
import { FilterPill, FilterPills } from '../../components/FilterPill'
import { EmptyBox, SkeletonRows } from '../../components/loaders'
import { Pager } from '../../components/Pager'
import { formatDateTime, formatNumber } from '../../lib/format'
import { DEFAULT_PAGE_SIZE, parsePageSize } from '../../lib/pageSize'
import { useLoadingDisplay } from '../../lib/useLoadingDisplay'
import { useCompactTables } from '../../lib/usePhone'
import { OrderModal } from './OrderModal'
import { lineName, shopHost, statusLabel, WOO_STATUSES } from './orderText'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

/** Products shown in a row before the rest are summed up as "još 2 proizvoda". */
const PRODUCTS_IN_ROW = 2

interface ListState {
  search: string
  page: number
  pageSize: number
  status: string | null
  unlinkedOnly: boolean
}

// Search, page and filters live in the URL, so back/forward and refresh keep them.
function toParams({ search, page, pageSize, status, unlinkedOnly }: ListState) {
  return {
    ...(search && { q: search }),
    ...(page > 1 && { page: String(page) }),
    ...(pageSize !== DEFAULT_PAGE_SIZE && { size: String(pageSize) }),
    ...(status && { status }),
    ...(unlinkedOnly && { unlinked: '1' }),
  }
}

export function OrdersPage() {
  const { t, i18n } = useTranslation()
  const compact = useCompactTables()
  const [params, setParams] = useSearchParams()
  const [openedId, setOpenedId] = useState<string | null>(null)
  const state: ListState = {
    search: params.get('q') ?? '',
    page: Math.max(1, Number(params.get('page')) || 1),
    pageSize: parsePageSize(params.get('size')),
    status: params.get('status'),
    unlinkedOnly: params.get('unlinked') === '1',
  }
  const update = (changes: Partial<ListState>, replace = false) =>
    setParams(toParams({ ...state, page: 1, ...changes }), { replace })

  const [searchInput, setSearchInput] = useState(state.search)
  const [debouncedSearch] = useDebouncedValue(searchInput, 300)
  useEffect(() => {
    // Reacts only to the typed text; the other filters change the URL directly.
    if (debouncedSearch.trim() !== state.search) update({ search: debouncedSearch.trim() }, true)
  }, [debouncedSearch]) // oxlint-disable-line react-hooks/exhaustive-deps

  const orders = useListOrders(
    {
      search: state.search || undefined,
      status: state.status ?? undefined,
      unlinkedOnly: state.unlinkedOnly || undefined,
      page: state.page,
      pageSize: state.pageSize,
    },
    { query: { placeholderData: keepPreviousData } },
  )
  const display = useLoadingDisplay(orders.isPending)

  const data = orders.data
  const searching = searchInput.trim() !== state.search || orders.isPlaceholderData
  const filtered = state.status !== null || state.unlinkedOnly
  // The row's data is the modal's data; it stays open while the list refreshes behind it.
  const opened = data?.items.find((o) => o.id === openedId)

  return (
    <Stack gap="lg">
      <Stack gap={4}>
        <Group gap="sm" align="baseline">
          <Title order={1} lts="-0.01em">
            {t('orders.title')}
          </Title>
          {data && !state.search && !filtered && (
            <Text ff="monospace" c="dimmed">
              {formatNumber(data.totalCount, i18n.language)}
            </Text>
          )}
        </Group>
        <Text fz="sm" c="dimmed">
          {t('orders.privacy')}
        </Text>
      </Stack>

      <Group gap="md" wrap="wrap">
        <TextInput
          flex="1 1 320px"
          type="search"
          aria-label={t('orders.search')}
          placeholder={t('orders.searchPlaceholder')}
          leftSection={<IconSearch size={16} stroke={1.8} />}
          rightSection={searching ? <Loader size={16} role="status" aria-label={t('orders.searching')} /> : null}
          value={searchInput}
          onChange={(e) => setSearchInput(e.currentTarget.value)}
        />
        <FilterPills label={t('orders.filters.label')}>
          <FilterPill active={!state.unlinkedOnly} onClick={() => update({ unlinkedOnly: false })}>
            {t('orders.filters.all')}
          </FilterPill>
          <FilterPill active={state.unlinkedOnly} onClick={() => update({ unlinkedOnly: true })}>
            {t('orders.filters.unlinked')}
          </FilterPill>
        </FilterPills>
        <Select
          w={200}
          aria-label={t('orders.filters.status')}
          placeholder={t('orders.filters.anyStatus')}
          data={WOO_STATUSES.map((status) => ({ value: status, label: statusLabel(t, status) }))}
          value={state.status}
          onChange={(status) => update({ status })}
          clearable
          allowDeselect
        />
      </Group>

      <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
        {orders.isPending || display !== 'content' ? (
          display === 'loader' && <SkeletonRows />
        ) : orders.isError ? (
          <Text role="alert" p="lg">
            {formErrorMessage(t, orders.error)}
          </Text>
        ) : data!.totalCount === 0 ? (
          <EmptyState search={state.search} filtered={filtered} />
        ) : (
          <>
            {compact ? (
              <OrdersList orders={data!.items} onOpen={setOpenedId} />
            ) : (
              <Table.ScrollContainer minWidth={760}>
                <OrdersTable orders={data!.items} onOpen={setOpenedId} />
              </Table.ScrollContainer>
            )}
            <Pager
              page={state.page}
              pageSize={state.pageSize}
              totalCount={data!.totalCount}
              onChange={(page) => update({ page })}
              onPageSizeChange={(pageSize) => update({ pageSize })}
            />
          </>
        )}
      </Box>

      {opened && <OrderModal order={opened} onClose={() => setOpenedId(null)} />}
    </Stack>
  )
}

function OrdersTable({ orders, onOpen }: { orders: OrderDto[]; onOpen: (id: string) => void }) {
  const { t, i18n } = useTranslation()
  const severalShops = new Set(orders.map((o) => o.channelId)).size > 1

  return (
    <Table>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>{t('orders.columns.order')}</Table.Th>
          <Table.Th>{t('orders.columns.placedAt')}</Table.Th>
          <Table.Th>{t('orders.columns.status')}</Table.Th>
          <Table.Th>{t('orders.columns.products')}</Table.Th>
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {orders.map((order) => (
          // The whole row opens the order for mouse users; the number is the real button for keyboards.
          <Table.Tr key={order.id} onClick={() => onOpen(order.id)} style={{ cursor: 'pointer' }}>
            <Table.Td>
              <OrderNumber order={order} onOpen={onOpen} />
              {severalShops && (
                <Text fz="xs" c="dimmed">
                  {shopHost(order.shopUrl)}
                </Text>
              )}
            </Table.Td>
            <Table.Td>
              <Text ff="monospace" fz="sm" c="var(--z-text-2)" style={{ whiteSpace: 'nowrap' }}>
                {formatDateTime(order.placedAt, i18n.language)}
              </Text>
            </Table.Td>
            <Table.Td>
              <Text fz="sm">{statusLabel(t, order.status)}</Text>
              <Text fz="xs" c="dimmed">
                {order.takesStock ? t('orders.takesStock') : t('orders.doesNotTakeStock')}
              </Text>
            </Table.Td>
            <Table.Td>
              <ProductsSummary order={order} />
            </Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  )
}

/**
 * The orders on a phone: one cell per order, number and time on top, status under them and the
 * products below, so nothing hides past the edge of the screen.
 */
function OrdersList({ orders, onOpen }: { orders: OrderDto[]; onOpen: (id: string) => void }) {
  const { t, i18n } = useTranslation()
  const severalShops = new Set(orders.map((o) => o.channelId)).size > 1

  return (
    <Table>
      <Table.Tbody>
        {orders.map((order) => (
          <Table.Tr key={order.id} onClick={() => onOpen(order.id)} style={{ cursor: 'pointer' }}>
            <Table.Td>
              <Group justify="space-between" gap="sm" wrap="nowrap" align="baseline">
                <OrderNumber order={order} onOpen={onOpen} />
                <Text ff="monospace" fz="xs" c="var(--z-text-2)" style={{ whiteSpace: 'nowrap' }}>
                  {formatDateTime(order.placedAt, i18n.language)}
                </Text>
              </Group>
              <Text fz="xs" c="dimmed" mb={6}>
                {[
                  statusLabel(t, order.status),
                  order.takesStock ? t('orders.takesStock') : t('orders.doesNotTakeStock'),
                  ...(severalShops ? [shopHost(order.shopUrl)] : []),
                ].join(' · ')}
              </Text>
              <ProductsSummary order={order} />
            </Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  )
}

/** The whole row opens the order for mouse and touch users; the number is the real button for keyboards. */
function OrderNumber({ order, onOpen }: { order: OrderDto; onOpen: (id: string) => void }) {
  const { t } = useTranslation()
  return (
    <Anchor
      component="button"
      type="button"
      onClick={(e) => {
        e.stopPropagation()
        onOpen(order.id)
      }}
      aria-label={t('orders.open', { number: order.number })}
      ff="monospace"
      fw={500}
      c="var(--z-text)"
    >
      {t('orders.number', { number: order.number })}
    </Anchor>
  )
}

function ProductsSummary({ order }: { order: OrderDto }) {
  const { t, i18n } = useTranslation()
  const shown = order.lines.slice(0, PRODUCTS_IN_ROW)
  const more = order.lines.length - shown.length

  return (
    <Stack gap={2}>
      {shown.map((line) => (
        <Text key={line.externalProductId} fz="sm" lineClamp={2} maw={480}>
          {lineName(t, line)}{' '}
          <Text component="span" ff="monospace" fz="sm" c="dimmed">
            ×{formatNumber(line.orderedQuantity, i18n.language)}
          </Text>
        </Text>
      ))}
      {(more > 0 || order.unlinkedLineCount > 0) && (
        <Group gap="sm">
          {more > 0 && (
            <Text fz="xs" c="dimmed">
              {t('orders.moreProducts', { count: more })}
            </Text>
          )}
          {order.unlinkedLineCount > 0 && (
            <Text fz="xs" c="var(--z-status-low)">
              {t('orders.unlinkedCount', { count: order.unlinkedLineCount })}
            </Text>
          )}
        </Group>
      )}
    </Stack>
  )
}

function EmptyState({ search, filtered }: { search: string; filtered: boolean }) {
  const { t } = useTranslation()
  if (search || filtered) {
    return (
      <Text p="lg" c="dimmed">
        {search ? t('orders.noResults', { search }) : t('orders.noFilterResults')}
      </Text>
    )
  }
  return (
    <Stack gap="sm" p="lg" align="flex-start">
      <EmptyBox />
      <Stack gap={4}>
        <Title order={2}>{t('orders.empty.title')}</Title>
        <Text c="dimmed">{t('orders.empty.description')}</Text>
      </Stack>
      <Button variant="default" component={Link} to="/channels">
        {t('orders.empty.channels')}
      </Button>
    </Stack>
  )
}
