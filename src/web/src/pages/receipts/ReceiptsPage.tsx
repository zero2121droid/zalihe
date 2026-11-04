import { Anchor, Box, Button, Group, Loader, Stack, Table, Text, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import type { ReceiptDto } from '../../api/generated/model'
import { useListReceipts } from '../../api/generated/receipts/receipts'
import { usePermissions } from '../../auth/permissions'
import { EmptyBox, SkeletonRows } from '../../components/loaders'
import { PageTurn } from '../../components/PageTurn'
import { Pager } from '../../components/Pager'
import { formatDate, formatMoney, formatNumber } from '../../lib/format'
import { DEFAULT_PAGE_SIZE, parsePageSize } from '../../lib/pageSize'
import { useLoadingDisplay } from '../../lib/useLoadingDisplay'
import { useCompactTables } from '../../lib/usePhone'
import { ReceiptModal } from './ReceiptModal'
import { documentLabel } from './receiptText'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

interface ListState {
  search: string
  page: number
  pageSize: number
}

// Search and page live in the URL, so back/forward and refresh keep them.
function toParams({ search, page, pageSize }: ListState) {
  return {
    ...(search && { q: search }),
    ...(page > 1 && { page: String(page) }),
    ...(pageSize !== DEFAULT_PAGE_SIZE && { size: String(pageSize) }),
  }
}

/** Posted receipts, newest first (SPEC 11.7). A row opens the receipt; "Novi prijem" enters a new one. */
export function ReceiptsPage() {
  const { t, i18n } = useTranslation()
  const compact = useCompactTables()
  const [params, setParams] = useSearchParams()
  // A receipt just posted opens by itself (?opened=…), so the user sees what went into stock.
  const [openedId, setOpenedId] = useState<string | null>(params.get('opened'))
  const state: ListState = {
    search: params.get('q') ?? '',
    page: Math.max(1, Number(params.get('page')) || 1),
    pageSize: parsePageSize(params.get('size')),
  }
  const update = (changes: Partial<ListState>, replace = false) => setParams(toParams({ ...state, page: 1, ...changes }), { replace })

  const [searchInput, setSearchInput] = useState(state.search)
  const [debouncedSearch] = useDebouncedValue(searchInput, 300)
  useEffect(() => {
    if (debouncedSearch.trim() !== state.search) update({ search: debouncedSearch.trim() }, true)
  }, [debouncedSearch]) // oxlint-disable-line react-hooks/exhaustive-deps

  const receipts = useListReceipts(
    { search: state.search || undefined, page: state.page, pageSize: state.pageSize },
    { query: { placeholderData: keepPreviousData } },
  )
  const display = useLoadingDisplay(receipts.isPending)
  const data = receipts.data
  const searching = searchInput.trim() !== state.search || receipts.isPlaceholderData
  const opened = data?.items.find((r) => r.id === openedId)

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-start" gap="md">
        <Stack gap={4}>
          <Group gap="sm" align="baseline">
            <Title order={1} lts="-0.01em">
              {t('receipts.title')}
            </Title>
            {data && !state.search && (
              <Text ff="monospace" c="dimmed">
                {formatNumber(data.totalCount, i18n.language)}
              </Text>
            )}
          </Group>
          <Text fz="sm" c="dimmed">
            {t('receipts.description')}
          </Text>
        </Stack>
        <Button component={Link} to="/receipts/new">
          {t('receipts.new')}
        </Button>
      </Group>

      <TextInput
        maw={520}
        type="search"
        aria-label={t('receipts.search')}
        placeholder={t('receipts.searchPlaceholder')}
        leftSection={<IconSearch size={16} stroke={1.8} />}
        rightSection={searching ? <Loader size={16} role="status" aria-label={t('receipts.searching')} /> : null}
        value={searchInput}
        onChange={(e) => setSearchInput(e.currentTarget.value)}
      />

      <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
        {receipts.isPending || display !== 'content' ? (
          display === 'loader' && <SkeletonRows />
        ) : receipts.isError ? (
          <Text role="alert" p="lg">
            {formErrorMessage(t, receipts.error)}
          </Text>
        ) : data!.totalCount === 0 ? (
          <EmptyState search={state.search} />
        ) : (
          <>
            <PageTurn page={data!.page}>
              <ReceiptsTable rows={data!.items} compact={compact} onOpen={setOpenedId} />
            </PageTurn>
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

      {opened && <ReceiptModal receipt={opened} onClose={() => setOpenedId(null)} />}
    </Stack>
  )
}

function ReceiptsTable({ rows, compact, onOpen }: { rows: ReceiptDto[]; compact: boolean; onOpen: (id: string) => void }) {
  const { t, i18n } = useTranslation()
  const prices = usePermissions().seePurchasePrices
  const currency = t('common.currency')

  if (compact) {
    // On a phone: one cell per receipt, supplier and date on top, document and lines below.
    return (
      <Table>
        <Table.Tbody>
          {rows.map((receipt) => (
            <Table.Tr key={receipt.id} onClick={() => onOpen(receipt.id)} style={{ cursor: 'pointer' }}>
              <Table.Td>
                <Group justify="space-between" gap="sm" wrap="nowrap" align="baseline">
                  <OpenButton receipt={receipt} onOpen={onOpen} />
                  <Text ff="monospace" fz="xs" c="var(--z-text-2)" style={{ whiteSpace: 'nowrap' }}>
                    {formatDate(receipt.documentDate, i18n.language)}
                  </Text>
                </Group>
                <Text fz="xs" c="dimmed">
                  {[
                    documentLabel(t, receipt),
                    t('receipts.form.lineCount', { count: receipt.lineCount }),
                    ...(prices && receipt.totalValue !== null ? [`${formatMoney(receipt.totalValue, i18n.language)} ${currency}`] : []),
                  ].join(' · ')}
                </Text>
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
    )
  }

  return (
    <Table>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>{t('receipts.columns.supplier')}</Table.Th>
          <Table.Th>{t('receipts.columns.document')}</Table.Th>
          <Table.Th>{t('receipts.columns.date')}</Table.Th>
          <Table.Th ta="right">{t('receipts.columns.lines')}</Table.Th>
          {prices && <Table.Th ta="right">{t('receipts.columns.value')}</Table.Th>}
          <Table.Th>{t('receipts.columns.enteredBy')}</Table.Th>
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {rows.map((receipt) => (
          <Table.Tr key={receipt.id} onClick={() => onOpen(receipt.id)} style={{ cursor: 'pointer' }}>
            <Table.Td>
              <OpenButton receipt={receipt} onOpen={onOpen} />
            </Table.Td>
            <Table.Td>
              <Text ff="monospace" fz="sm" c={receipt.documentNumber ? 'var(--z-text-2)' : 'dimmed'}>
                {documentLabel(t, receipt)}
              </Text>
            </Table.Td>
            <Table.Td>
              <Text ff="monospace" fz="sm" c="var(--z-text-2)" style={{ whiteSpace: 'nowrap' }}>
                {formatDate(receipt.documentDate, i18n.language)}
              </Text>
            </Table.Td>
            <Table.Td ta="right">
              <Text ff="monospace" fz="sm">
                {formatNumber(receipt.lineCount, i18n.language)}
              </Text>
            </Table.Td>
            {prices && (
              <Table.Td ta="right">
                <Text ff="monospace" fz="sm" style={{ whiteSpace: 'nowrap' }}>
                  {receipt.totalValue === null ? '—' : formatMoney(receipt.totalValue, i18n.language)}
                </Text>
              </Table.Td>
            )}
            <Table.Td>
              <Text fz="sm" c="dimmed">
                {receipt.createdByName ?? ''}
              </Text>
            </Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  )
}

/** The whole row opens the receipt for mouse users; the supplier is the real button for keyboards. */
function OpenButton({ receipt, onOpen }: { receipt: ReceiptDto; onOpen: (id: string) => void }) {
  const { t } = useTranslation()
  return (
    <Anchor
      component="button"
      type="button"
      onClick={(e) => {
        e.stopPropagation()
        onOpen(receipt.id)
      }}
      aria-label={t('receipts.open', { supplier: receipt.supplierName, document: documentLabel(t, receipt) })}
      fz="sm"
      fw={500}
      c="var(--z-text)"
      ta="left"
    >
      {receipt.supplierName}
    </Anchor>
  )
}

function EmptyState({ search }: { search: string }) {
  const { t } = useTranslation()
  if (search) {
    return (
      <Text p="lg" c="dimmed">
        {t('receipts.noResults', { search })}
      </Text>
    )
  }
  return (
    <Stack gap="sm" p="lg" align="flex-start">
      <EmptyBox />
      <Stack gap={4}>
        <Title order={2}>{t('receipts.empty.title')}</Title>
        <Text c="dimmed">{t('receipts.empty.description')}</Text>
      </Stack>
      <Button component={Link} to="/receipts/new">
        {t('receipts.new')}
      </Button>
    </Stack>
  )
}
