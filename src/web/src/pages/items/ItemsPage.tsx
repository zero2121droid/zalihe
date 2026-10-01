import {
  ActionIcon,
  Badge,
  Box,
  Button,
  Center,
  Group,
  Loader,
  Stack,
  Switch,
  Table,
  Text,
  TextInput,
  Title,
  UnstyledButton,
} from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconChevronLeft, IconChevronRight, IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import { useListItems } from '../../api/generated/items/items'
import type { ItemDto } from '../../api/generated/model'
import { formatNumber } from '../../lib/format'
import { ItemModal } from './ItemModal'

const PAGE_SIZE = 20

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }
// Mantine drops the border of disabled buttons; the mockup keeps it on both arrows.
const pagerButtonStyle = { border: '1px solid var(--z-line)' }

export function ItemsPage() {
  const { t, i18n } = useTranslation()
  // null = closed, 'new' = create form, an item = edit form.
  const [editing, setEditing] = useState<ItemDto | 'new' | null>(null)
  // Search, page and the inactive filter live in the URL, so back/forward and refresh keep them.
  const [params, setParams] = useSearchParams()
  const search = params.get('q') ?? ''
  const page = Math.max(1, Number(params.get('page')) || 1)
  const showInactive = params.get('inactive') === '1'

  const [searchInput, setSearchInput] = useState(search)
  const [debouncedSearch] = useDebouncedValue(searchInput, 300)
  useEffect(() => {
    if (debouncedSearch.trim() === search) return
    setParams(buildParams(debouncedSearch.trim(), 1, showInactive), { replace: true })
  }, [debouncedSearch, search, showInactive, setParams])

  const items = useListItems(
    { search: search || undefined, includeInactive: showInactive || undefined, page, pageSize: PAGE_SIZE },
    { query: { placeholderData: keepPreviousData } },
  )

  function goToPage(next: number) {
    setParams(buildParams(search, next, showInactive))
  }

  const data = items.data
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-end" gap="md">
        <Group gap="sm" align="baseline">
          <Title order={1} lts="-0.01em">
            {t('items.title')}
          </Title>
          {data && !search && (
            <Text ff="monospace" c="dimmed">
              {formatNumber(data.totalCount, i18n.language)}
            </Text>
          )}
        </Group>
        <Button onClick={() => setEditing('new')}>{t('items.new')}</Button>
      </Group>

      <Group gap="lg" wrap="wrap">
        <TextInput
          flex="1 1 320px"
          type="search"
          aria-label={t('items.search')}
          placeholder={t('items.searchPlaceholder')}
          leftSection={<IconSearch size={16} stroke={1.8} />}
          value={searchInput}
          onChange={(e) => setSearchInput(e.currentTarget.value)}
        />
        <Switch
          label={t('items.showInactive')}
          checked={showInactive}
          onChange={(e) => setParams(buildParams(search, 1, e.currentTarget.checked))}
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
          <EmptyState search={search} onAdd={() => setEditing('new')} />
        ) : (
          <>
            <Table.ScrollContainer minWidth={560}>
              <ItemsTable items={data!.items} onOpen={setEditing} />
            </Table.ScrollContainer>
            <Group justify="space-between" px="lg" py="sm">
              <Text fz="sm" c="dimmed">
                {t('items.range', {
                  from: (page - 1) * PAGE_SIZE + 1,
                  to: Math.min(page * PAGE_SIZE, data!.totalCount),
                  total: data!.totalCount,
                })}
              </Text>
              <Group gap={6}>
                <ActionIcon
                  variant="default"
                  size={32}
                  style={pagerButtonStyle}
                  aria-label={t('common.previousPage')}
                  disabled={page <= 1}
                  onClick={() => goToPage(page - 1)}
                >
                  <IconChevronLeft size={14} stroke={2} />
                </ActionIcon>
                <ActionIcon
                  variant="default"
                  size={32}
                  style={pagerButtonStyle}
                  aria-label={t('common.nextPage')}
                  disabled={page >= totalPages}
                  onClick={() => goToPage(page + 1)}
                >
                  <IconChevronRight size={14} stroke={2} />
                </ActionIcon>
              </Group>
            </Group>
          </>
        )}
      </Box>

      <ItemModal
        key={editing === 'new' ? 'new' : (editing?.id ?? 'closed')}
        opened={editing !== null}
        onClose={() => setEditing(null)}
        item={editing === 'new' ? null : editing}
      />
    </Stack>
  )
}

function buildParams(search: string, page: number, showInactive: boolean) {
  return {
    ...(search && { q: search }),
    ...(page > 1 && { page: String(page) }),
    ...(showInactive && { inactive: '1' }),
  }
}

function ItemsTable({ items, onOpen }: { items: ItemDto[]; onOpen: (item: ItemDto) => void }) {
  const { t, i18n } = useTranslation()
  return (
    <Table>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>{t('items.columns.item')}</Table.Th>
          <Table.Th>{t('items.columns.sku')}</Table.Th>
          <Table.Th ta="right">{t('items.columns.minStock')}</Table.Th>
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {items.map((item) => (
          // The whole row opens the item for mouse users; the name is the real button for keyboards.
          <Table.Tr key={item.id} onClick={() => onOpen(item)} style={{ cursor: 'pointer' }}>
            <Table.Td>
              <Group gap="xs" wrap="nowrap">
                <UnstyledButton
                  onClick={(e) => {
                    e.stopPropagation()
                    onOpen(item)
                  }}
                  aria-label={t('items.edit', { name: item.name })}
                  className="z-focus"
                >
                  <Text fw={500} truncate maw={420} c={item.isActive ? undefined : 'dimmed'}>
                    {item.name}
                  </Text>
                </UnstyledButton>
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
              <Text component="span" ff="monospace" fz="sm" c="dimmed" style={{ fontVariantNumeric: 'tabular-nums' }}>
                {formatNumber(item.minStock, i18n.language)}{' '}
                <Text component="span" fz={11}>
                  {t(`items.units.${item.unit}`)}
                </Text>
              </Text>
            </Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  )
}

function EmptyState({ search, onAdd }: { search: string; onAdd: () => void }) {
  const { t } = useTranslation()
  if (search) {
    return (
      <Text p="lg" c="dimmed">
        {t('items.noResults', { search })}
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
