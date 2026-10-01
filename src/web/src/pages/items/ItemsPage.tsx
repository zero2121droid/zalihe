import { ActionIcon, Anchor, Badge, Box, Button, Center, Group, Loader, Stack, Switch, Table, Text, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconChevronLeft, IconChevronRight, IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import { useListItems } from '../../api/generated/items/items'
import type { ItemDto } from '../../api/generated/model'
import { useGetStockSummary } from '../../api/generated/stock/stock'
import { FilterPill } from '../../components/FilterPill'
import { StockQuantity, StockStatusLabel } from '../../components/stock'
import { formatMoney, formatNumber } from '../../lib/format'
import { ItemModal } from './ItemModal'

const PAGE_SIZE = 20

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }
// Mantine drops the border of disabled buttons; the mockup keeps it on both arrows.
const pagerButtonStyle = { border: '1px solid var(--z-line)' }

type StatusFilter = 'low' | 'outOfStock' | null

interface ListState {
  search: string
  page: number
  showInactive: boolean
  status: StatusFilter
}

// Search, page and filters live in the URL, so back/forward and refresh keep them.
function toParams({ search, page, showInactive, status }: ListState) {
  return {
    ...(search && { q: search }),
    ...(page > 1 && { page: String(page) }),
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
      pageSize: PAGE_SIZE,
    },
    { query: { placeholderData: keepPreviousData } },
  )
  const summary = useGetStockSummary()

  const data = items.data
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

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
        <Button onClick={() => setCreating(true)}>{t('items.new')}</Button>
      </Group>

      <Group gap="md" wrap="wrap">
        <TextInput
          flex="1 1 320px"
          type="search"
          aria-label={t('items.search')}
          placeholder={t('items.searchPlaceholder')}
          leftSection={<IconSearch size={16} stroke={1.8} />}
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
        {items.isPending ? (
          <Center py="xl">
            <Loader size="sm" aria-label={t('common.loading')} />
          </Center>
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
            <Group justify="space-between" px="lg" py="sm">
              <Text fz="sm" c="dimmed">
                {t('items.range', {
                  from: (state.page - 1) * PAGE_SIZE + 1,
                  to: Math.min(state.page * PAGE_SIZE, data!.totalCount),
                  total: data!.totalCount,
                })}
              </Text>
              <Group gap={6}>
                <ActionIcon
                  variant="default"
                  size={32}
                  style={pagerButtonStyle}
                  aria-label={t('common.previousPage')}
                  disabled={state.page <= 1}
                  onClick={() => update({ page: state.page - 1 })}
                >
                  <IconChevronLeft size={14} stroke={2} />
                </ActionIcon>
                <ActionIcon
                  variant="default"
                  size={32}
                  style={pagerButtonStyle}
                  aria-label={t('common.nextPage')}
                  disabled={state.page >= totalPages}
                  onClick={() => update({ page: state.page + 1 })}
                >
                  <IconChevronRight size={14} stroke={2} />
                </ActionIcon>
              </Group>
            </Group>
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
              <StockQuantity quantity={item.stock} unit={item.unit} status={item.status} />
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
