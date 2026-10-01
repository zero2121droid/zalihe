import { ActionIcon, Anchor, Badge, Box, Button, Center, Group, Loader, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core'
import { IconArrowLeft, IconChevronLeft, IconChevronRight } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import type { TFunction } from 'i18next'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { ApiError, formErrorMessage } from '../../api/errors'
import { useGetItem, useGetItemHistory } from '../../api/generated/items/items'
import type { ItemDto, ItemHistoryFilter, ManualMovementKind } from '../../api/generated/model'
import { FilterPill } from '../../components/FilterPill'
import { MovementQuantity, StockQuantity, StockStatusLabel } from '../../components/stock'
import { formatDateTime, formatMoney, formatNumber } from '../../lib/format'
import { ItemModal } from './ItemModal'
import { MovementModal } from './MovementModal'

const HISTORY_PAGE_SIZE = 20
const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }
const pagerButtonStyle = { border: '1px solid var(--z-line)' }
const movementKinds: ManualMovementKind[] = ['receipt', 'sale', 'return', 'count']

export function ItemDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const item = useGetItem(id)

  if (item.isPending) {
    return (
      <Center py="xl">
        <Loader size="sm" aria-label={t('common.loading')} />
      </Center>
    )
  }

  if (item.isError) {
    const notFound = item.error instanceof ApiError && item.error.status === 404
    return (
      <Stack gap="md" align="flex-start">
        <BackLink />
        <Text role="alert">{notFound ? t('items.detail.notFound') : formErrorMessage(t, item.error)}</Text>
      </Stack>
    )
  }

  return <ItemDetail item={item.data} />
}

function BackLink() {
  const { t } = useTranslation()
  return (
    <Anchor component={Link} to="/items" fz="sm">
      <Group gap={6} component="span">
        <IconArrowLeft size={14} stroke={1.8} />
        {t('items.title')}
      </Group>
    </Anchor>
  )
}

function ItemDetail({ item }: { item: ItemDto }) {
  const { t, i18n } = useTranslation()
  const [movementKind, setMovementKind] = useState<ManualMovementKind | null>(null)
  const [editing, setEditing] = useState(false)

  return (
    <Stack gap="lg">
      <Stack gap="xs">
        <BackLink />
        <Group justify="space-between" align="flex-end" gap="md">
          <Stack gap={4} miw={0}>
            <Group gap="sm">
              <Title order={1} lts="-0.01em">
                {item.name}
              </Title>
              {!item.isActive && (
                <Badge color="gray" c="dimmed">
                  {t('items.inactive')}
                </Badge>
              )}
            </Group>
            <Text fz="sm" c="dimmed">
              <Text component="span" ff="monospace" fz="sm">
                {item.sku}
              </Text>
              {item.category && ` · ${item.category}`}
            </Text>
          </Stack>
          <Group gap="sm">
            <Button variant="default" onClick={() => setEditing(true)}>
              {t('items.detail.edit')}
            </Button>
            {movementKinds.map((kind) => (
              <Button key={kind} variant={kind === 'receipt' ? 'filled' : 'default'} onClick={() => setMovementKind(kind)}>
                {t(`stock.actions.${kind}`)}
              </Button>
            ))}
          </Group>
        </Group>
      </Stack>

      {/* One strip divided by lines, not separate cards (DESIGN.md): the 1 px gaps show the line color. */}
      <SimpleGrid
        cols={{ base: 2, md: 4 }}
        spacing={1}
        verticalSpacing={1}
        bg="var(--z-line)"
        bd="1px solid var(--z-line)"
        style={{ ...sectionStyle, overflow: 'hidden' }}
      >
        <Figure label={t('items.columns.stock')}>
          <StockQuantity quantity={item.stock} unit={item.unit} status={item.status} size={26} />
          <StockStatusLabel status={item.status} />
        </Figure>
        <Figure label={t('items.detail.minStock')}>
          <StockQuantity quantity={item.minStock} unit={item.unit} status="inStock" size={26} />
        </Figure>
        <Figure label={t('items.detail.sold30Days')}>
          <StockQuantity quantity={item.sold30Days} unit={item.unit} status="inStock" size={26} />
        </Figure>
        <Figure label={t('items.detail.value')}>
          {item.stockValue === null ? (
            <Text c="dimmed" fz="sm">
              {t('items.detail.noPrice')}
            </Text>
          ) : (
            <Text ff="monospace" fz={26} fw={500} style={{ fontVariantNumeric: 'tabular-nums' }}>
              {formatMoney(item.stockValue, i18n.language)}{' '}
              <Text component="span" fz={13} c="dimmed" fw={400}>
                {t('common.currency')}
              </Text>
            </Text>
          )}
        </Figure>
      </SimpleGrid>

      <History item={item} />

      <MovementModal key={movementKind ?? 'closed'} item={item} kind={movementKind} onClose={() => setMovementKind(null)} />
      {editing && <ItemModal opened item={item} onClose={() => setEditing(false)} />}
    </Stack>
  )
}

function Figure({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Stack gap={6} p="md" px="lg" bg="var(--z-surface)">
      <Text fz="xs" c="dimmed">
        {label}
      </Text>
      {children}
    </Stack>
  )
}

const historyFilters: ItemHistoryFilter[] = ['all', 'stock', 'changes']

function History({ item }: { item: ItemDto }) {
  const { t, i18n } = useTranslation()
  const [filter, setFilter] = useState<ItemHistoryFilter>('all')
  const [page, setPage] = useState(1)
  const history = useGetItemHistory(
    item.id,
    { filter: filter === 'all' ? undefined : filter, page, pageSize: HISTORY_PAGE_SIZE },
    { query: { placeholderData: keepPreviousData } },
  )
  const data = history.data
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / HISTORY_PAGE_SIZE)) : 1

  return (
    <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Group justify="space-between" px="lg" py="sm" gap="sm" style={{ borderBottom: '1px solid var(--z-line)' }}>
        <Title order={2}>{t('stock.history.title')}</Title>
        <Group gap={6} role="group" aria-label={t('stock.history.filters.label')}>
          {historyFilters.map((value) => (
            <FilterPill
              key={value}
              active={filter === value}
              onClick={() => {
                setFilter(value)
                setPage(1)
              }}
            >
              {t(`stock.history.filters.${value}`)}
            </FilterPill>
          ))}
        </Group>
      </Group>

      {history.isPending ? (
        <Center py="xl">
          <Loader size="sm" aria-label={t('common.loading')} />
        </Center>
      ) : history.isError ? (
        <Text role="alert" p="lg">
          {formErrorMessage(t, history.error)}
        </Text>
      ) : data!.totalCount === 0 ? (
        <Text p="lg" c="dimmed">
          {t('stock.history.empty')}
        </Text>
      ) : (
        <>
          <Table.ScrollContainer minWidth={720}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t('stock.history.columns.when')}</Table.Th>
                  <Table.Th>{t('stock.history.columns.type')}</Table.Th>
                  <Table.Th>{t('stock.history.columns.details')}</Table.Th>
                  <Table.Th ta="right">{t('stock.history.columns.quantity')}</Table.Th>
                  <Table.Th>{t('stock.history.columns.user')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data!.items.map((entry) => (
                  <Table.Tr key={entry.id}>
                    <Table.Td>
                      <Text ff="monospace" fz="xs" c="dimmed" style={{ whiteSpace: 'nowrap' }}>
                        {formatDateTime(entry.occurredAt, i18n.language)}
                      </Text>
                    </Table.Td>
                    {entry.movement ? (
                      <>
                        <Table.Td>
                          <Text fz="sm">{t(`stock.types.${entry.movement.type}`)}</Text>
                          <Text fz="xs" c="dimmed">
                            {t(`stock.sources.${entry.movement.source}`)}
                            {entry.movement.externalRef && ` #${entry.movement.externalRef}`}
                          </Text>
                        </Table.Td>
                        <Table.Td>
                          <Text fz="sm" c="var(--z-text-2)">
                            {entry.movement.note}
                          </Text>
                        </Table.Td>
                        <Table.Td ta="right">
                          <MovementQuantity quantity={entry.movement.quantity} />
                        </Table.Td>
                      </>
                    ) : (
                      <>
                        <Table.Td>
                          <Text fz="sm">{t(`items.changes.${entry.change!.kind}`)}</Text>
                        </Table.Td>
                        <Table.Td>
                          <Stack gap={2}>
                            {entry.change!.changes.map((field) => (
                              <Text key={field.field} fz="sm" c="var(--z-text-2)">
                                <Text component="span" fz="sm" c="dimmed">
                                  {t(`items.form.${field.field}`)}:
                                </Text>{' '}
                                {formatChangeValue(field.field, field.oldValue, item, t, i18n.language)} →{' '}
                                {formatChangeValue(field.field, field.newValue, item, t, i18n.language)}
                              </Text>
                            ))}
                          </Stack>
                        </Table.Td>
                        <Table.Td />
                      </>
                    )}
                    <Table.Td>
                      <Text fz="sm" c="dimmed">
                        {entry.userName}
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <Group justify="space-between" px="lg" py="sm">
            <Text fz="sm" c="dimmed">
              {t('items.range', {
                from: formatNumber((page - 1) * HISTORY_PAGE_SIZE + 1, i18n.language),
                to: formatNumber(Math.min(page * HISTORY_PAGE_SIZE, data!.totalCount), i18n.language),
                total: formatNumber(data!.totalCount, i18n.language),
              })}
            </Text>
            <Group gap={6}>
              <ActionIcon
                variant="default"
                size={32}
                style={pagerButtonStyle}
                aria-label={t('common.previousPage')}
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
              >
                <IconChevronLeft size={14} stroke={2} />
              </ActionIcon>
              <ActionIcon
                variant="default"
                size={32}
                style={pagerButtonStyle}
                aria-label={t('common.nextPage')}
                disabled={page >= totalPages}
                onClick={() => setPage(page + 1)}
              >
                <IconChevronRight size={14} stroke={2} />
              </ActionIcon>
            </Group>
          </Group>
        </>
      )}
    </Box>
  )
}

/**
 * A logged value for display. The API stores values language-neutral ("12.5", "kom");
 * here they get the user's number format, translated units and the currency.
 */
function formatChangeValue(field: string, value: string | null, item: ItemDto, t: TFunction, language: string): string {
  if (value === null) return t('stock.history.emptyValue')
  switch (field) {
    case 'unit':
      return t(`items.units.${value}`)
    case 'minStock':
      return `${formatNumber(Number(value), language)} ${t(`items.units.${item.unit}`)}`
    case 'purchasePrice':
    case 'salePrice':
      return `${formatMoney(Number(value), language)} ${t('common.currency')}`
    default:
      return value
  }
}
