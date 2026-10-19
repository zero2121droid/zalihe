import { Anchor, Box, Group, Loader, Select, SimpleGrid, Stack, Table, Text, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconSearch } from '@tabler/icons-react'
import { keepPreviousData, type UseQueryResult } from '@tanstack/react-query'
import { type ReactNode, useEffect, useState } from 'react'
import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import { useListItemCategories } from '../../api/generated/items/items'
import type { CategoryValueDto, SlowMoverDto, TopSellerDto } from '../../api/generated/model'
import { useGetReportSummary, useListCategoryValues, useListSlowMovers, useListTopSellers } from '../../api/generated/reports/reports'
import { FilterPill } from '../../components/FilterPill'
import { SkeletonRows } from '../../components/loaders'
import { Pager } from '../../components/Pager'
import { SearchBody, SearchCell, SearchMatch, SearchRow } from '../../components/search'
import { StockQuantity, StockStatusLabel } from '../../components/stock'
import { formatDateTime, formatMoney, formatNumber, formatPercent } from '../../lib/format'
import { DEFAULT_PAGE_SIZE, parsePageSize } from '../../lib/pageSize'
import { matchesSearch } from '../../lib/search'
import { useLoadingDisplay } from '../../lib/useLoadingDisplay'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }
const number = { ff: 'monospace', fz: 'sm', style: { fontVariantNumeric: 'tabular-nums' } } as const

/** The periods the API reports over (ReportService.TopSellerDays and SlowMoverDays). */
const TOP_SELLER_DAYS = 30
const SLOW_MOVER_DAYS = 60

const REPORTS = ['topSellers', 'slowMovers', 'stockValue'] as const
type Report = (typeof REPORTS)[number]

interface ReportState {
  report: Report
  search: string
  category: string | null
  page: number
  pageSize: number
}

// The report, search, filter and page live in the URL, so back/forward and refresh keep them.
function toParams({ report, search, category, page, pageSize }: ReportState) {
  return {
    ...(report !== 'topSellers' && { r: report }),
    ...(search && { q: search }),
    ...(category && { cat: category }),
    ...(page > 1 && { page: String(page) }),
    ...(pageSize !== DEFAULT_PAGE_SIZE && { size: String(pageSize) }),
  }
}

function parseReport(value: string | null): Report {
  return REPORTS.find((r) => r === value) ?? 'topSellers'
}

export function ReportsPage() {
  const { t, i18n } = useTranslation()
  const [params, setParams] = useSearchParams()
  const state: ReportState = {
    report: parseReport(params.get('r')),
    search: params.get('q') ?? '',
    category: params.get('cat'),
    page: Math.max(1, Number(params.get('page')) || 1),
    pageSize: parsePageSize(params.get('size')),
  }
  const update = (changes: Partial<ReportState>, replace = false) =>
    setParams(toParams({ ...state, page: 1, ...changes }), { replace })

  const [searchInput, setSearchInput] = useState(state.search)
  const [debouncedSearch] = useDebouncedValue(searchInput, 300)
  useEffect(() => {
    // Reacts only to the typed text; the other filters change the URL directly.
    if (debouncedSearch.trim() !== state.search) update({ search: debouncedSearch.trim() }, true)
  }, [debouncedSearch]) // oxlint-disable-line react-hooks/exhaustive-deps

  // The value per category report covers every category, so it has no search or category filter.
  const filterable = state.report !== 'stockValue'
  const category = filterable ? state.category : null
  const summary = useGetReportSummary({ category: category ?? undefined }, { query: { placeholderData: keepPreviousData } })
  const categories = useListItemCategories()

  const changeReport = (report: Report) => {
    setSearchInput('')
    update({ report, search: '', category: report === 'stockValue' ? null : state.category })
  }

  const list = { search: state.search, category, page: state.page, pageSize: state.pageSize }
  const pager = { onChange: (page: number) => update({ page }), onPageSizeChange: (pageSize: number) => update({ pageSize }) }

  return (
    <Stack gap="lg">
      <Stack gap={4}>
        <Title order={1} lts="-0.01em">
          {t('reports.title')}
        </Title>
        <Text fz="sm" c="dimmed">
          {t('reports.description')}
        </Text>
      </Stack>

      {/* One strip divided by lines, not separate cards (DESIGN.md): the 1 px gaps show the line color. */}
      <SimpleGrid
        component="section"
        aria-label={t('reports.figures.label')}
        cols={{ base: 2, md: 4 }}
        spacing={1}
        verticalSpacing={1}
        bg="var(--z-line)"
        bd="1px solid var(--z-line)"
        style={{ ...sectionStyle, overflow: 'hidden' }}
      >
        <Figure label={t('reports.figures.stockValue')} hint={t('reports.figures.stockValueHint')}>
          {summary.data && <Money value={summary.data.stockValue} />}
        </Figure>
        <Figure label={t('reports.figures.withoutPrice')} hint={t('reports.figures.withoutPriceHint')}>
          {summary.data && formatNumber(summary.data.itemsWithoutPrice, i18n.language)}
        </Figure>
        <Figure label={t('reports.figures.slowMovers', { days: SLOW_MOVER_DAYS })} hint={t('reports.figures.slowMoversHint')}>
          {summary.data && formatNumber(summary.data.slowMoverCount, i18n.language)}
        </Figure>
        <Figure label={t('reports.figures.slowMoverValue')} hint={t('reports.figures.slowMoverValueHint')}>
          {summary.data && <Money value={summary.data.slowMoverValue} />}
        </Figure>
      </SimpleGrid>

      <Group gap="md" wrap="wrap">
        <Group gap={6} role="group" aria-label={t('reports.choose')}>
          {REPORTS.map((report) => (
            <FilterPill key={report} active={state.report === report} onClick={() => changeReport(report)}>
              {t(`reports.${report}.tab`, { days: report === 'topSellers' ? TOP_SELLER_DAYS : SLOW_MOVER_DAYS })}
            </FilterPill>
          ))}
        </Group>
        {filterable && (
          <>
            <TextInput
              flex="1 1 280px"
              type="search"
              aria-label={t('reports.search')}
              placeholder={t('reports.searchPlaceholder')}
              leftSection={<IconSearch size={16} stroke={1.8} />}
              value={searchInput}
              onChange={(e) => setSearchInput(e.currentTarget.value)}
              rightSection={
                searchInput.trim() !== state.search ? <Loader size={16} role="status" aria-label={t('reports.searching')} /> : null
              }
            />
            <Select
              w={220}
              aria-label={t('reports.category')}
              placeholder={t('reports.anyCategory')}
              data={categories.data ?? []}
              value={state.category}
              onChange={(value) => update({ category: value })}
              searchable
              clearable
            />
          </>
        )}
      </Group>

      {state.report === 'topSellers' && <TopSellers {...list} searchInput={searchInput} {...pager} />}
      {state.report === 'slowMovers' && <SlowMovers {...list} searchInput={searchInput} {...pager} />}
      {state.report === 'stockValue' && (
        <StockValue page={state.page} pageSize={state.pageSize} total={summary.data?.stockValue} {...pager} />
      )}
    </Stack>
  )
}

function Money({ value }: { value: number }) {
  const { t, i18n } = useTranslation()
  return (
    <>
      {formatMoney(value, i18n.language)}{' '}
      <Text component="span" fz={13} c="dimmed" fw={400}>
        {t('common.currency')}
      </Text>
    </>
  )
}

function Figure({ label, hint, children }: { label: string; hint: string; children: ReactNode }) {
  return (
    <Stack gap={6} p="md" px="lg" bg="var(--z-surface)">
      <Text fz="xs" c="dimmed">
        {label}
      </Text>
      <Text ff="monospace" fz={26} fw={500} lts="-0.02em" mih={39} style={{ fontVariantNumeric: 'tabular-nums' }}>
        {children}
      </Text>
      <Text fz="xs" c="dimmed">
        {hint}
      </Text>
    </Stack>
  )
}

interface ListProps {
  search: string
  /** What is typed now; the rows of this page that can't match fold away until the results come. */
  searchInput: string
  category: string | null
  page: number
  pageSize: number
  onChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

interface Paged<T> {
  items: T[]
  totalCount: number
}

/** The report's section: title, then a loader, the error, the empty text or the table with its pager. */
function ReportSection<T>({
  title,
  description,
  query,
  empty,
  page,
  pageSize,
  onChange,
  onPageSizeChange,
  children,
}: {
  title: string
  description: string
  query: UseQueryResult<Paged<T>, unknown>
  empty: string
  page: number
  pageSize: number
  onChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
  children: (rows: T[]) => ReactNode
}) {
  const { t } = useTranslation()
  const display = useLoadingDisplay(query.isPending)

  return (
    <Box component="section" aria-label={title} bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Stack gap={2} px="lg" py="md" style={{ borderBottom: '1px solid var(--z-line)' }}>
        <Title order={2}>{title}</Title>
        <Text fz="xs" c="dimmed">
          {description}
        </Text>
      </Stack>
      {query.isPending || display !== 'content' ? (
        display === 'loader' && <SkeletonRows />
      ) : query.isError ? (
        <Text role="alert" p="lg">
          {formErrorMessage(t, query.error)}
        </Text>
      ) : query.data!.totalCount === 0 ? (
        <Text p="lg" c="dimmed">
          {empty}
        </Text>
      ) : (
        <>
          <Table.ScrollContainer minWidth={640}>{children(query.data!.items)}</Table.ScrollContainer>
          <Pager
            page={page}
            pageSize={pageSize}
            totalCount={query.data!.totalCount}
            onChange={onChange}
            onPageSizeChange={onPageSizeChange}
          />
        </>
      )}
    </Box>
  )
}

/** Empty text for a filtered list, or `none` when nothing is filtered. */
function emptyText(t: TFunction, search: string, category: string | null, none: string) {
  if (search) return t('reports.noResults', { search })
  if (category) return t('reports.noCategoryResults', { category })
  return none
}

function ItemCell({ itemId, name, sku, search, hidden }: { itemId: string; name: string; sku: string; search: string; hidden: boolean }) {
  const { t } = useTranslation()
  return (
    <Table.Td>
      <SearchCell>
        <Anchor
          component={Link}
          to={`/items/${itemId}`}
          tabIndex={hidden ? -1 : undefined}
          aria-label={t('items.open', { name })}
          fw={500}
          c="var(--z-text)"
          truncate
          maw={420}
          display="block"
        >
          <SearchMatch text={name} term={search} />
        </Anchor>
        <Text ff="monospace" fz={11} c="dimmed">
          <SearchMatch text={sku} term={search} />
        </Text>
      </SearchCell>
    </Table.Td>
  )
}

function TopSellers({ search, searchInput, category, page, pageSize, ...pager }: ListProps) {
  const { t, i18n } = useTranslation()
  const query = useListTopSellers(
    { search: search || undefined, category: category ?? undefined, page, pageSize },
    { query: { placeholderData: keepPreviousData } },
  )
  const narrowing = searchInput.trim() !== search || query.isPlaceholderData

  return (
    <ReportSection<TopSellerDto>
      title={t('reports.topSellers.title', { days: TOP_SELLER_DAYS })}
      description={t('reports.topSellers.description')}
      query={query}
      empty={emptyText(t, search, category, t('reports.topSellers.none', { days: TOP_SELLER_DAYS }))}
      page={page}
      pageSize={pageSize}
      {...pager}
    >
      {(rows) => (
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={56} ta="right">
                {t('reports.columns.rank')}
              </Table.Th>
              <Table.Th>{t('items.columns.item')}</Table.Th>
              <Table.Th ta="right">{t('reports.columns.sold')}</Table.Th>
              <Table.Th ta="right">{t('items.columns.stock')}</Table.Th>
              <Table.Th>{t('items.columns.status')}</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <SearchBody narrowing={narrowing}>
            {rows.map((row, index) => {
              const hidden = !matchesSearch(searchInput, row.name, row.sku)
              return (
                <SearchRow key={row.itemId} hidden={hidden}>
                  <Table.Td ta="right">
                    <SearchCell>
                      <Text {...number} c="dimmed">
                        {formatNumber((page - 1) * pageSize + index + 1, i18n.language)}
                      </Text>
                    </SearchCell>
                  </Table.Td>
                  <ItemCell itemId={row.itemId} name={row.name} sku={row.sku} search={searchInput} hidden={hidden} />
                  <Table.Td ta="right">
                    <SearchCell>
                      <StockQuantity quantity={row.sold} unit={row.unit} status="inStock" />
                    </SearchCell>
                  </Table.Td>
                  <Table.Td ta="right">
                    <SearchCell>
                      <StockQuantity quantity={row.stock} unit={row.unit} status={row.status} />
                    </SearchCell>
                  </Table.Td>
                  <Table.Td>
                    <SearchCell>
                      <StockStatusLabel status={row.status} />
                    </SearchCell>
                  </Table.Td>
                </SearchRow>
              )
            })}
          </SearchBody>
        </Table>
      )}
    </ReportSection>
  )
}

function SlowMovers({ search, searchInput, category, page, pageSize, ...pager }: ListProps) {
  const { t, i18n } = useTranslation()
  const query = useListSlowMovers(
    { search: search || undefined, category: category ?? undefined, page, pageSize },
    { query: { placeholderData: keepPreviousData } },
  )
  const narrowing = searchInput.trim() !== search || query.isPlaceholderData

  return (
    <ReportSection<SlowMoverDto>
      title={t('reports.slowMovers.title', { days: SLOW_MOVER_DAYS })}
      description={t('reports.slowMovers.description', { days: SLOW_MOVER_DAYS })}
      query={query}
      empty={emptyText(t, search, category, t('reports.slowMovers.none', { days: SLOW_MOVER_DAYS }))}
      page={page}
      pageSize={pageSize}
      {...pager}
    >
      {(rows) => (
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>{t('items.columns.item')}</Table.Th>
              <Table.Th ta="right">{t('items.columns.stock')}</Table.Th>
              <Table.Th ta="right">{t('items.columns.value')}</Table.Th>
              <Table.Th>{t('reports.columns.lastSold')}</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <SearchBody narrowing={narrowing}>
            {rows.map((row) => {
              const hidden = !matchesSearch(searchInput, row.name, row.sku)
              return (
                <SearchRow key={row.itemId} hidden={hidden}>
                  <ItemCell itemId={row.itemId} name={row.name} sku={row.sku} search={searchInput} hidden={hidden} />
                  <Table.Td ta="right">
                    <SearchCell>
                      <StockQuantity quantity={row.stock} unit={row.unit} status="inStock" />
                    </SearchCell>
                  </Table.Td>
                  <Table.Td ta="right">
                    <SearchCell>
                      {row.value === null ? (
                        <Text fz="xs" c="dimmed">
                          {t('items.detail.noPrice')}
                        </Text>
                      ) : (
                        <Text {...number} c="var(--z-text-2)">
                          {formatMoney(row.value, i18n.language)}
                        </Text>
                      )}
                    </SearchCell>
                  </Table.Td>
                  <Table.Td>
                    <SearchCell>
                      <Text fz="sm" c="var(--z-text-2)" ff={row.lastSoldAt ? 'monospace' : undefined} style={{ whiteSpace: 'nowrap' }}>
                        {row.lastSoldAt ? formatDateTime(row.lastSoldAt, i18n.language) : t('reports.slowMovers.neverSold')}
                      </Text>
                    </SearchCell>
                  </Table.Td>
                </SearchRow>
              )
            })}
          </SearchBody>
        </Table>
      )}
    </ReportSection>
  )
}

function StockValue({
  page,
  pageSize,
  total,
  ...pager
}: Pick<ListProps, 'page' | 'pageSize' | 'onChange' | 'onPageSizeChange'> & { total: number | undefined }) {
  const { t, i18n } = useTranslation()
  const query = useListCategoryValues({ page, pageSize }, { query: { placeholderData: keepPreviousData } })

  return (
    <ReportSection<CategoryValueDto>
      title={t('reports.stockValue.title')}
      description={t('reports.stockValue.description')}
      query={query}
      empty={t('reports.stockValue.none')}
      page={page}
      pageSize={pageSize}
      {...pager}
    >
      {(rows) => (
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>{t('reports.columns.category')}</Table.Th>
              <Table.Th ta="right">{t('reports.columns.items')}</Table.Th>
              <Table.Th ta="right">{t('items.columns.value')}</Table.Th>
              <Table.Th w={200}>{t('reports.columns.share')}</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {rows.map((row) => {
              const share = total ? row.value / total : 0
              return (
                <Table.Tr key={row.category ?? ''}>
                  <Table.Td>
                    <Text fw={500} c={row.category ? undefined : 'dimmed'}>
                      {row.category ?? t('reports.stockValue.noCategory')}
                    </Text>
                    {row.itemsWithoutPrice > 0 && (
                      <Text fz="xs" c="dimmed">
                        {t('reports.stockValue.withoutPrice', { count: row.itemsWithoutPrice })}
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td ta="right">
                    <Text {...number} c="var(--z-text-2)">
                      {formatNumber(row.itemCount, i18n.language)}
                    </Text>
                  </Table.Td>
                  <Table.Td ta="right">
                    <Text ff="monospace" fz={18} fw={500} style={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                      <Money value={row.value} />
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Group gap="sm" wrap="nowrap">
                      <Box h={4} flex={1} bg="var(--z-line)" style={{ borderRadius: 2 }}>
                        <Box h={4} w={`${share * 100}%`} bg="var(--z-accent)" style={{ borderRadius: 2 }} />
                      </Box>
                      <Text {...number} fz="xs" c="dimmed" w={44} ta="right">
                        {formatPercent(share, i18n.language)}
                      </Text>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              )
            })}
          </Table.Tbody>
        </Table>
      )}
    </ReportSection>
  )
}
