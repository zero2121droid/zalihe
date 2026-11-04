import { Badge, Box, Group, Loader, SimpleGrid, Stack, Table, Text, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import type { PlatformCompanyDto } from '../../api/generated/model'
import { useGetPlatformSummary, useListPlatformCompanies } from '../../api/generated/platform/platform'
import { EmptyBox, SkeletonRows } from '../../components/loaders'
import { PageTurn } from '../../components/PageTurn'
import { Pager } from '../../components/Pager'
import { formatDateTime, formatNumber } from '../../lib/format'
import { DEFAULT_PAGE_SIZE, parsePageSize } from '../../lib/pageSize'
import { useLoadingDisplay } from '../../lib/useLoadingDisplay'
import { useCompactTables } from '../../lib/usePhone'

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

/** How the companies use Lagerko, for the platform's owner (SPEC 11.8): totals, and who got stuck where. */
export function PlatformPage() {
  const { t } = useTranslation()
  const compact = useCompactTables()
  const [params, setParams] = useSearchParams()
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

  const companies = useListPlatformCompanies(
    { search: state.search || undefined, page: state.page, pageSize: state.pageSize },
    { query: { placeholderData: keepPreviousData } },
  )
  const display = useLoadingDisplay(companies.isPending)
  const data = companies.data
  const searching = searchInput.trim() !== state.search || companies.isPlaceholderData

  return (
    <Stack gap="lg">
      <Stack gap={4}>
        <Title order={1} lts="-0.01em">
          {t('platform.title')}
        </Title>
        <Text fz="sm" c="dimmed">
          {t('platform.description')}
        </Text>
      </Stack>

      <Summary />

      <TextInput
        maw={520}
        type="search"
        aria-label={t('platform.search')}
        placeholder={t('platform.searchPlaceholder')}
        leftSection={<IconSearch size={16} stroke={1.8} />}
        rightSection={searching ? <Loader size={16} role="status" aria-label={t('platform.searching')} /> : null}
        value={searchInput}
        onChange={(e) => setSearchInput(e.currentTarget.value)}
      />

      <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
        {companies.isPending || display !== 'content' ? (
          display === 'loader' && <SkeletonRows />
        ) : companies.isError ? (
          <Text role="alert" p="lg">
            {formErrorMessage(t, companies.error)}
          </Text>
        ) : data!.totalCount === 0 ? (
          <EmptyState search={state.search} />
        ) : (
          <>
            <PageTurn page={data!.page}>
              <CompaniesTable rows={data!.items} compact={compact} />
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
    </Stack>
  )
}

function Summary() {
  const { t } = useTranslation()
  const summary = useGetPlatformSummary()

  if (summary.isError) {
    return <Text role="alert">{formErrorMessage(t, summary.error)}</Text>
  }

  const data = summary.data
  return (
    <SimpleGrid
      component="section"
      aria-label={t('platform.summary')}
      className="z-kpi"
      cols={{ base: 2, md: 4 }}
      spacing={1}
      verticalSpacing={1}
      bg="var(--z-line)"
      bd="1px solid var(--z-line)"
      style={{ ...sectionStyle, overflow: 'hidden' }}
    >
      <Figure label={t('platform.figures.companies')} hint={t('platform.figures.companiesHint')} value={data?.companies} />
      <Figure label={t('platform.figures.new')} hint={t('platform.figures.last7Days')} value={data?.newLast7Days} />
      <Figure label={t('platform.figures.active')} hint={t('platform.figures.activeHint')} value={data?.activeLast7Days} />
      <Figure label={t('platform.figures.withShop')} hint={t('platform.figures.withShopHint')} value={data?.withShop} />
    </SimpleGrid>
  )
}

function Figure({ label, hint, value }: { label: string; hint: string; value: number | undefined }) {
  const { i18n } = useTranslation()
  return (
    <Stack gap={6} p="md" px="lg" bg="var(--z-surface)">
      <Text fz="xs" c="dimmed">
        {label}
      </Text>
      <Text ff="monospace" fz={26} fw={500} lts="-0.02em" style={{ fontVariantNumeric: 'tabular-nums' }}>
        {value === undefined ? '—' : formatNumber(value, i18n.language)}
      </Text>
      <Text fz="xs" c="dimmed">
        {hint}
      </Text>
    </Stack>
  )
}

function CompaniesTable({ rows, compact }: { rows: PlatformCompanyDto[]; compact: boolean }) {
  const { t, i18n } = useTranslation()
  const shop = (company: PlatformCompanyDto) => (company.hasShop ? t('platform.shopConnected') : t('platform.noShop'))
  const lastMovement = (company: PlatformCompanyDto) =>
    company.lastMovementAt ? formatDateTime(company.lastMovementAt, i18n.language) : t('platform.noMovements')

  if (compact) {
    // On a phone: one cell per company, name and date on top, the rest below.
    return (
      <Table>
        <Table.Tbody>
          {rows.map((company) => (
            <Table.Tr key={company.id}>
              <Table.Td>
                <Group justify="space-between" gap="sm" wrap="nowrap" align="baseline">
                  <CompanyName company={company} />
                  <Text ff="monospace" fz="xs" c="var(--z-text-2)" style={{ whiteSpace: 'nowrap' }}>
                    {formatDateTime(company.registeredAt, i18n.language)}
                  </Text>
                </Group>
                <Text fz="xs" c="dimmed">
                  {[t('platform.itemCount', { count: company.activeItems }), shop(company), lastMovement(company)].join(' · ')}
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
          <Table.Th>{t('platform.columns.company')}</Table.Th>
          <Table.Th>{t('platform.columns.registered')}</Table.Th>
          <Table.Th ta="right">{t('platform.columns.items')}</Table.Th>
          <Table.Th>{t('platform.columns.shop')}</Table.Th>
          <Table.Th>{t('platform.columns.lastMovement')}</Table.Th>
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {rows.map((company) => (
          <Table.Tr key={company.id}>
            <Table.Td>
              <CompanyName company={company} />
            </Table.Td>
            <Table.Td>
              <Text ff="monospace" fz="sm" c="var(--z-text-2)" style={{ whiteSpace: 'nowrap' }}>
                {formatDateTime(company.registeredAt, i18n.language)}
              </Text>
            </Table.Td>
            <Table.Td ta="right">
              <Text ff="monospace" fz="sm">
                {formatNumber(company.activeItems, i18n.language)}
              </Text>
            </Table.Td>
            <Table.Td>
              <Text fz="sm" c={company.hasShop ? 'var(--z-text-2)' : 'dimmed'}>
                {shop(company)}
              </Text>
            </Table.Td>
            <Table.Td>
              <Text ff="monospace" fz="sm" c={company.lastMovementAt ? 'var(--z-text-2)' : 'dimmed'} style={{ whiteSpace: 'nowrap' }}>
                {lastMovement(company)}
              </Text>
            </Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  )
}

function CompanyName({ company }: { company: PlatformCompanyDto }) {
  const { t } = useTranslation()
  return (
    <Group gap="xs" wrap="nowrap">
      <Text fz="sm" fw={500}>
        {company.name}
      </Text>
      {company.isOwn && (
        <Badge size="sm" variant="default" tt="none" fw={400}>
          {t('platform.yours')}
        </Badge>
      )}
    </Group>
  )
}

function EmptyState({ search }: { search: string }) {
  const { t } = useTranslation()
  if (search) {
    return (
      <Text p="lg" c="dimmed">
        {t('platform.noResults', { search })}
      </Text>
    )
  }
  return (
    <Stack gap="sm" p="lg" align="flex-start">
      <EmptyBox />
      <Text fz="sm" c="dimmed">
        {t('platform.empty')}
      </Text>
    </Stack>
  )
}
