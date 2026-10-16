import { Anchor, Badge, Box, Button, Group, Loader, Stack, Switch, Table, Text, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import { useListItems } from '../../api/generated/items/items'
import type { ItemDto } from '../../api/generated/model'
import { useGetStockSummary } from '../../api/generated/stock/stock'
import { FilterPill } from '../../components/FilterPill'
import { EmptyBox, SkeletonRows } from '../../components/loaders'
import { Pager } from '../../components/Pager'
import { StockQuantity, StockStatusLabel } from '../../components/stock'
import { formatMoney, formatNumber } from '../../lib/format'
import { DEFAULT_PAGE_SIZE, parsePageSize } from '../../lib/pageSize'
import { useLoadingDisplay } from '../../lib/useLoadingDisplay'
import { ItemModal } from './ItemModal'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

type StatusFilter = 'low' | 'outOfStock' | null

interface ListState {
  search: string
  page: number
  pageSize: number
  showInactive: boolean
  status: StatusFilter
}

// Search, page and filters live in the URL, so back/forward and refresh keep them.
function toParams({ search, page, pageSize, showInactive, status }: ListState) {
  return {
    ...(search && { q: search }),
    ...(page > 1 && { page: String(page) }),
    ...(pageSize !== DEFAULT_PAGE_SIZE && { size: String(pageSize) }),
    ...(showInactive && { inactive: '1' }),
    ...(status && { status }),
  }
}

export function ItemsPage() {
  const { t, i18n } = useTranslation()
  const [creating, setCreating] = useState(false)
  const [params, setParams] = useSearchParams()
  const statusParam = params.get('status')
  const state: ListState = {
    search: params.get('q') ?? '',
    page: Math.max(1, Number(params.get('page')) || 1),
    pageSize: parsePageSize(params.get('size')),
    showInactive: params.get('inactive') === '1',
    status: statusParam === 'low' || statusParam === 'outOfStock' ? statusParam : null,
  }
  const update = (changes: Partial<ListState>, replace = false) =>
    setParams(toParams({ ...state, page: 1, ...changes }), { replace })

  const [searchInput, setSearchInput] = useState(state.search)
  const [debouncedSearch] = useDebouncedValue(searchInput, 300)
  useEffect(() => {
    // Reacts only to the typed text; the other filters change the URL directly.
    if (debouncedSearch.trim() !== state.search) update({ search: debouncedSearch.trim() }, true)
  }, [debouncedSearch]) // oxlint-disable-line react-hooks/exhaustive-deps

  const items = useListItems(
    {
      search: state.search || undefined,
      includeInactive: state.showInactive || undefined,
      status: state.status ?? undefined,
      page: state.page,
      pageSize: state.pageSize,
    },
    { query: { placeholderData: keepPreviousData } },
  )
  const summary = useGetStockSummary()
  const display = useLoadingDisplay(items.isPending)

  const data = items.data
  // Typed text not yet sent (debounce) or new results on their way: the old rows stay on screen,
  // and a small loader in the search field says the list is about to change.
  const searching = searchInput.trim() !== state.search || items.isPlaceholderData

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-end" gap="md">
        <Group gap="sm" align="baseline">
          <Title order={1} lts="-0.01em">
            {t('items.title')}
          </Title>
          {data && !state.search && !state.status && (
            <Text ff="monospace" c="dimmed">
              {formatNumber(data.totalCount, i18n.language)}
            </Text>
          )}
        </Group>
        <Group gap="sm">
          <Button variant="default" component={Link} to="/items/import">
            {t('items.importCsv')}
          </Button>
          <Button onClick={() => setCreating(true)}>{t('items.new')}</Button>
        </Group>
      </Group>

      <Group gap="md" wrap="wrap">
        <TextInput
          flex="1 1 320px"
          type="search"
          aria-label={t('items.search')}
          placeholder={t('items.searchPlaceholder')}
          leftSection={<IconSearch size={16} stroke={1.8} />}
          rightSection={searching ? <Loader size={16} role="status" aria-label={t('items.searching')} /> : null}
          value={searchInput}
          onChange={(e) => setSearchInput(e.currentTarget.value)}
        />
        <Group gap={6} role="group" aria-label={t('items.filters.label')}>
          <FilterPill active={state.status === null} onClick={() => update({ status: null })}>
            {t('items.filters.all')}
          </FilterPill>
          <FilterPill active={state.status === 'low'} onClick={() => update({ status: 'low' })}>
            {t('items.filters.low')}{' '}
            <PillCount color="var(--z-status-low)" value={summary.data?.belowMinimum} />
          </FilterPill>
          <FilterPill active={state.status === 'outOfStock'} onClick={() => update({ status: 'outOfStock' })}>
            {t('items.filters.outOfStock')}{' '}
            <PillCount color="var(--z-status-out)" value={summary.data?.outOfStock} />
          </FilterPill>
        </Group>
        <Switch
          label={t('items.showInactive')}
          checked={state.showInactive}
          onChange={(e) => update({ showInactive: e.currentTarget.checked })}
        />
      </Group>

      <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
        {items.isPending || display !== 'content' ? (
          display === 'loader' && <SkeletonRows />
        ) : items.isError ? (
          <Text role="alert" p="lg">
            {formErrorMessage(t, items.error)}
          </Text>
        ) : data!.totalCount === 0 ? (
          <EmptyState search={state.search} filtered={state.status !== null} onAdd={() => setCreating(true)} />
        ) : (
          <>
            <Table.ScrollContainer minWidth={900}>
              <ItemsTable items={data!.items} />
            </Table.ScrollContainer>
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

      {creating && <ItemModal opened onClose={() => setCreating(false)} />}
    </Stack>
  )
}

function PillCount({ color, value }: { color: string; value: number | undefined }) {
  const { i18n } = useTranslation()
  if (value === undefined) return null
  return (
    <Text component="span" ff="monospace" fz="sm" c={color}>
      {formatNumber(value, i18n.language)}
    </Text>
  )
}

function ItemsTable({ items }: { items: ItemDto[] }) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const number = { ff: 'monospace', fz: 'sm', style: { fontVariantNumeric: 'tabular-nums' } } as const

  return (
    <Table>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>{t('items.columns.item')}</Table.Th>
          <Table.Th>{t('items.columns.sku')}</Table.Th>
          <Table.Th ta="right">{t('items.columns.stock')}</Table.Th>
          <Table.Th ta="right">{t('items.columns.minStock')}</Table.Th>
          <Table.Th>{t('items.columns.status')}</Table.Th>
          <Table.Th ta="right">{t('items.columns.sold30Days')}</Table.Th>
          <Table.Th ta="right">{t('items.columns.value')}</Table.Th>
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {items.map((item) => (
          // The whole row opens the item for mouse users; the name is the real link for keyboards.
          <Table.Tr key={item.id} onClick={() => void navigate(`/items/${item.id}`)} style={{ cursor: 'pointer' }}>
            <Table.Td>
              <Group gap="xs" wrap="nowrap">
                <Anchor
                  component={Link}
                  to={`/items/${item.id}`}
                  onClick={(e) => e.stopPropagation()}
                  aria-label={t('items.open', { name: item.name })}
                  fw={500}
                  c={item.isActive ? 'var(--z-text)' : 'dimmed'}
                  truncate
                  maw={420}
                >
                  {item.name}
                </Anchor>
                {!item.isActive && (
                  <Badge size="sm" color="gray" c="dimmed">
                    {t('items.inactive')}
                  </Badge>
                )}
              </Group>
              {item.category && (
                <Text fz="xs" c="dimmed">
                  {item.category}
                </Text>
              )}
            </Table.Td>
            <Table.Td>
              <Text ff="monospace" fz="xs" c="var(--z-text-2)">
                {item.sku}
              </Text>
            </Table.Td>
            <Table.Td ta="right">
              <StockQuantity quantity={item.stock} unit={item.unit} status={item.status} animate />
            </Table.Td>
            <Table.Td ta="right">
              <Text {...number} c="dimmed">
                {formatNumber(item.minStock, i18n.language)}
              </Text>
            </Table.Td>
            <Table.Td>
              <StockStatusLabel status={item.status} />
            </Table.Td>
            <Table.Td ta="right">
              <Text {...number}>{formatNumber(item.sold30Days, i18n.language)}</Text>
            </Table.Td>
            <Table.Td ta="right">
              <Text {...number} c="var(--z-text-2)">
                {item.stockValue === null ? '' : formatMoney(item.stockValue, i18n.language)}
              </Text>
            </Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  )
}

function EmptyState({ search, filtered, onAdd }: { search: string; filtered: boolean; onAdd: () => void }) {
  const { t } = useTranslation()
  if (search || filtered) {
    return (
      <Text p="lg" c="dimmed">
        {search ? t('items.noResults', { search }) : t('items.noFilterResults')}
      </Text>
    )
  }
  return (
    <Stack gap="sm" p="lg" align="flex-start">
      <EmptyBox />
      <Stack gap={4}>
        <Title order={2}>{t('items.empty.title')}</Title>
        <Text c="dimmed">{t('items.empty.description')}</Text>
      </Stack>
      <Button variant="default" onClick={onAdd}>
        {t('items.new')}
      </Button>
    </Stack>
  )
}
