import { Anchor, Badge, Box, Button, Group, Loader, Stack, Switch, Table, Text, TextInput, Title, UnstyledButton } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconArrowDown, IconArrowUp, IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import { useListItems } from '../../api/generated/items/items'
import { type ItemDto, ItemSort, SortDirection } from '../../api/generated/model'
import { useGetStockSummary } from '../../api/generated/stock/stock'
import { FilterPill, FilterPills } from '../../components/FilterPill'
import { ItemImage } from '../../components/ItemImage'
import { EmptyBox, SkeletonRows } from '../../components/loaders'
import { Pager } from '../../components/Pager'
import { SearchBody, SearchCell, SearchMatch, SearchRow } from '../../components/search'
import { StockQuantity, StockStatusLabel } from '../../components/stock'
import { formatMoney, formatNumber } from '../../lib/format'
import { DEFAULT_PAGE_SIZE, parsePageSize } from '../../lib/pageSize'
import { matchesSearch } from '../../lib/search'
import { useFlipRows } from '../../lib/useFlipRows'
import { useLoadingDisplay } from '../../lib/useLoadingDisplay'
import { useCompactTables } from '../../lib/usePhone'
import { ItemModal } from './ItemModal'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

type StatusFilter = 'low' | 'outOfStock' | null

interface ListState {
  search: string
  page: number
  pageSize: number
  showInactive: boolean
  status: StatusFilter
  sort: ItemSort
  direction: SortDirection
}

interface Sorting {
  sort: ItemSort
  direction: SortDirection
  onSort: (column: ItemSort) => void
}

// Search, page, filters and sorting live in the URL, so back/forward and refresh keep them.
function toParams({ search, page, pageSize, showInactive, status, sort, direction }: ListState) {
  return {
    ...(search && { q: search }),
    ...(page > 1 && { page: String(page) }),
    ...(pageSize !== DEFAULT_PAGE_SIZE && { size: String(pageSize) }),
    ...(showInactive && { inactive: '1' }),
    ...(status && { status }),
    ...(sort !== ItemSort.name && { sort }),
    ...(direction !== SortDirection.asc && { dir: direction }),
  }
}

const sortValues = Object.values(ItemSort) as string[]

/** A new column starts where it says the most: names A to Z, numbers from the largest. */
const firstDirection = (column: ItemSort) => (column === ItemSort.name ? SortDirection.asc : SortDirection.desc)

export function ItemsPage() {
  const { t, i18n } = useTranslation()
  const compact = useCompactTables('lg')
  const [params, setParams] = useSearchParams()
  // `?new=1` (the home page's "New item") opens the form once; the address drops it, so back doesn't reopen it.
  const [creating, setCreating] = useState(() => params.get('new') === '1')
  useEffect(() => {
    if (params.has('new')) {
      params.delete('new')
      setParams(params, { replace: true })
    }
  }, []) // oxlint-disable-line react-hooks/exhaustive-deps
  const statusParam = params.get('status')
  const state: ListState = {
    search: params.get('q') ?? '',
    page: Math.max(1, Number(params.get('page')) || 1),
    pageSize: parsePageSize(params.get('size')),
    showInactive: params.get('inactive') === '1',
    status: statusParam === 'low' || statusParam === 'outOfStock' ? statusParam : null,
    sort: sortValues.includes(params.get('sort') ?? '') ? (params.get('sort') as ItemSort) : ItemSort.name,
    direction: params.get('dir') === SortDirection.desc ? SortDirection.desc : SortDirection.asc,
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
      sort: state.sort === ItemSort.name ? undefined : state.sort,
      direction: state.direction === SortDirection.asc ? undefined : state.direction,
    },
    { query: { placeholderData: keepPreviousData } },
  )
  const sorting: Sorting = {
    sort: state.sort,
    direction: state.direction,
    onSort: (column) =>
      update({
        sort: column,
        direction:
          column === state.sort
            ? state.direction === SortDirection.asc
              ? SortDirection.desc
              : SortDirection.asc
            : firstDirection(column),
      }),
  }
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
        <FilterPills label={t('items.filters.label')}>
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
        </FilterPills>
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
            {compact ? (
              <ItemsTable items={data!.items} search={searchInput} narrowing={searching} compact sorting={sorting} />
            ) : (
              <Table.ScrollContainer minWidth={900}>
                <ItemsTable items={data!.items} search={searchInput} narrowing={searching} compact={false} sorting={sorting} />
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

/**
 * `search` is the text in the field right now: while the server looks for it (`narrowing`), rows
 * of this page that can't match fold away, and the typed text is marked in names and SKUs.
 * In compact tables (a phone or a tablet) only the item and its stock are columns: the SKU and the status go under the name
 * (in place of the category), so the name has room and the number stays in view without scrolling.
 */
function ItemsTable({
  items,
  search,
  narrowing,
  compact,
  sorting,
}: {
  items: ItemDto[]
  search: string
  narrowing: boolean
  compact: boolean
  sorting: Sorting
}) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const number = { ff: 'monospace', fz: 'sm', style: { fontVariantNumeric: 'tabular-nums' } } as const
  const table = useRef<HTMLTableElement>(null)
  useFlipRows(
    table,
    items.map((item) => item.id),
    `${sorting.sort}:${sorting.direction}`,
  )

  return (
    // On a phone the stock column keeps its width and a long name wraps instead.
    <Table ref={table} layout={compact ? 'fixed' : undefined}>
      <Table.Thead>
        <Table.Tr>
          <SortableTh column={ItemSort.name} sorting={sorting}>
            {t('items.columns.item')}
          </SortableTh>
          {!compact && <Table.Th>{t('items.columns.sku')}</Table.Th>}
          <SortableTh column={ItemSort.stock} sorting={sorting} right w={compact ? 100 : undefined}>
            {t('items.columns.stock')}
          </SortableTh>
          {!compact && (
            <>
              <Table.Th ta="right">{t('items.columns.minStock')}</Table.Th>
              <Table.Th>{t('items.columns.status')}</Table.Th>
              <SortableTh column={ItemSort.sold30Days} sorting={sorting} right>
                {t('items.columns.sold30Days')}
              </SortableTh>
              <SortableTh column={ItemSort.value} sorting={sorting} right>
                {t('items.columns.value')}
              </SortableTh>
            </>
          )}
        </Table.Tr>
      </Table.Thead>
      <SearchBody narrowing={narrowing}>
        {items.map((item) => {
          const hidden = !matchesSearch(search, item.name, item.sku, item.barcode)
          return (
            // The whole row opens the item for mouse users; the name is the real link for keyboards.
            <SearchRow
              key={item.id}
              data-flip={item.id}
              hidden={hidden}
              onClick={() => void navigate(`/items/${item.id}`)}
              style={{ cursor: 'pointer' }}
            >
              <Table.Td>
                <SearchCell>
                  <Group gap="sm" wrap="nowrap">
                    <ItemImage url={item.imageUrl} size={36} />
                    <Box miw={0}>
                      <Group gap="xs" wrap="nowrap">
                        <Anchor
                          component={Link}
                          to={`/items/${item.id}`}
                          onClick={(e) => e.stopPropagation()}
                          tabIndex={hidden ? -1 : undefined}
                          aria-label={t('items.open', { name: item.name })}
                          fw={500}
                          c={item.isActive ? 'var(--z-text)' : 'dimmed'}
                          truncate={!compact}
                          maw={compact ? undefined : 420}
                        >
                          <SearchMatch text={item.name} term={search} />
                        </Anchor>
                        {!item.isActive && (
                          <Badge size="sm" color="gray" c="dimmed">
                            {t('items.inactive')}
                          </Badge>
                        )}
                      </Group>
                      {compact && (
                        <Text ff="monospace" fz="xs" c="var(--z-text-2)" truncate>
                          <SearchMatch text={item.sku} term={search} />
                        </Text>
                      )}
                      {compact ? (
                        <Box mt={2}>
                          <StockStatusLabel status={item.status} />
                        </Box>
                      ) : (
                        item.category && (
                          <Text fz="xs" c="dimmed">
                            {item.category}
                          </Text>
                        )
                      )}
                    </Box>
                  </Group>
                </SearchCell>
              </Table.Td>
              {!compact && (
                <Table.Td>
                  <SearchCell>
                    <Text ff="monospace" fz="xs" c="var(--z-text-2)" style={{ whiteSpace: 'nowrap' }}>
                      <SearchMatch text={item.sku} term={search} />
                    </Text>
                  </SearchCell>
                </Table.Td>
              )}
              <Table.Td ta="right">
                <SearchCell>
                  <StockQuantity quantity={item.stock} unit={item.unit} status={item.status} animate />
                </SearchCell>
              </Table.Td>
              {!compact && (
                <>
                  <Table.Td ta="right">
                    <SearchCell>
                      <Text {...number} c="dimmed">
                        {formatNumber(item.minStock, i18n.language)}
                      </Text>
                    </SearchCell>
                  </Table.Td>
                  <Table.Td>
                    <SearchCell>
                      <StockStatusLabel status={item.status} />
                    </SearchCell>
                  </Table.Td>
                  <Table.Td ta="right">
                    <SearchCell>
                      <Text {...number}>{formatNumber(item.sold30Days, i18n.language)}</Text>
                    </SearchCell>
                  </Table.Td>
                  <Table.Td ta="right">
                    <SearchCell>
                      <Text {...number} c="var(--z-text-2)">
                        {item.stockValue === null ? '' : formatMoney(item.stockValue, i18n.language)}
                      </Text>
                    </SearchCell>
                  </Table.Td>
                </>
              )}
            </SearchRow>
          )
        })}
      </SearchBody>
    </Table>
  )
}

/**
 * A column heading that sorts the list: a click sorts by it, another click turns the order around.
 * The arrow shows the current direction; screen readers get it from aria-sort.
 */
function SortableTh({
  column,
  sorting,
  right,
  w,
  children,
}: {
  column: ItemSort
  sorting: Sorting
  right?: boolean
  w?: number
  children: string
}) {
  const { t } = useTranslation()
  const active = sorting.sort === column
  const Arrow = sorting.direction === SortDirection.asc ? IconArrowUp : IconArrowDown
  return (
    <Table.Th
      ta={right ? 'right' : undefined}
      w={w}
      aria-sort={active ? (sorting.direction === SortDirection.asc ? 'ascending' : 'descending') : undefined}
    >
      <UnstyledButton
        onClick={() => sorting.onSort(column)}
        className="z-focus"
        title={t('items.sortBy', { column: children })}
        style={{
          font: 'inherit',
          letterSpacing: 'inherit',
          textTransform: 'inherit',
          color: active ? 'var(--z-text)' : 'inherit',
          display: 'inline-flex',
          alignItems: 'center',
          gap: 4,
          flexDirection: right ? 'row-reverse' : 'row',
        }}
      >
        {children}
        {/* Always takes its place, so the heading doesn't shift when it becomes the sorted one. */}
        <Arrow size={12} stroke={1.8} aria-hidden style={{ visibility: active ? 'visible' : 'hidden' }} />
      </UnstyledButton>
    </Table.Th>
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
