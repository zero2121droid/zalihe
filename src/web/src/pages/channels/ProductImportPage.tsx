import { Anchor, Box, Button, Center, Checkbox, Group, Loader, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core'
import { IconArrowLeft } from '@tabler/icons-react'
import { useQueryClient } from '@tanstack/react-query'
import { type ReactNode, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import {
  getPreviewProductImportQueryKey,
  useImportProducts,
  usePreviewProductImport,
} from '../../api/generated/channels/channels'
import { getGetDashboardQueryKey } from '../../api/generated/dashboard/dashboard'
import { getListItemsQueryKey } from '../../api/generated/items/items'
import type { ProductImportPreviewDto, ProductImportResultDto } from '../../api/generated/model'
import { getGetStockSummaryQueryKey } from '../../api/generated/stock/stock'
import { formatMoney, formatNumber } from '../../lib/format'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

/**
 * Imports the shop's products: links those whose SKU already exists and creates the new ones the
 * user keeps checked. The shop is read once for the preview and again by the import itself.
 */
export function ProductImportPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const queryClient = useQueryClient()
  const [result, setResult] = useState<ProductImportResultDto | null>(null)
  // Reading a shop is slow and costs it requests: don't read it again on focus or remount.
  const preview = usePreviewProductImport(id, { query: { staleTime: Infinity, refetchOnWindowFocus: false } })
  const runImport = useImportProducts({
    mutation: {
      onSuccess: async (data) => {
        setResult(data)
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetStockSummaryQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetDashboardQueryKey() }),
          queryClient.removeQueries({ queryKey: getPreviewProductImportQueryKey(id) }),
        ])
      },
    },
  })

  return (
    <Stack gap="lg">
      <Stack gap="xs">
        <Anchor component={Link} to="/channels" fz="sm">
          <Group gap={6} component="span">
            <IconArrowLeft size={14} stroke={1.8} />
            {t('channels.title')}
          </Group>
        </Anchor>
        <Title order={1} lts="-0.01em">
          {t('channels.import.title')}
        </Title>
      </Stack>

      {result ? (
        <Done result={result} />
      ) : preview.isPending ? (
        <Center py="xl">
          <Group gap="xs">
            <Loader size="sm" />
            <Text c="dimmed">{t('channels.import.reading')}</Text>
          </Group>
        </Center>
      ) : preview.isError ? (
        <Section>
          <Stack gap="md" align="flex-start">
            <Text role="alert" c="var(--mantine-color-error)">
              {formErrorMessage(t, preview.error)}
            </Text>
            <Button variant="default" onClick={() => preview.refetch()} loading={preview.isFetching}>
              {t('channels.import.retry')}
            </Button>
          </Stack>
        </Section>
      ) : (
        <Preview
          preview={preview.data}
          importing={runImport.isPending}
          error={formErrorMessage(t, runImport.error)}
          onImport={(create) => runImport.mutate({ id, data: { createExternalIds: create } })}
        />
      )}
    </Stack>
  )
}

function Section({ title, note, children }: { title?: string; note?: string; children: ReactNode }) {
  return (
    <Box component="section" aria-label={title} p="lg" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Stack gap="sm">
        {title && <Title order={2}>{title}</Title>}
        {note && (
          <Text fz="sm" c="dimmed">
            {note}
          </Text>
        )}
        {children}
      </Stack>
    </Box>
  )
}

interface PreviewProps {
  preview: ProductImportPreviewDto
  importing: boolean
  error: string | null
  onImport: (createExternalIds: string[]) => void
}

function Preview({ preview, importing, error, onImport }: PreviewProps) {
  const { t, i18n } = useTranslation()
  // Every new product starts checked; the user unchecks what shouldn't become an item.
  const [unchecked, setUnchecked] = useState<Set<string>>(new Set())
  const toCreate = preview.toCreate.filter((p) => !unchecked.has(p.externalId))
  const allChecked = toCreate.length === preview.toCreate.length
  const nothingToDo = preview.toCreate.length === 0 && preview.toLink.length === 0

  function toggle(externalId: string, checked: boolean) {
    const next = new Set(unchecked)
    if (checked) next.delete(externalId)
    else next.add(externalId)
    setUnchecked(next)
  }

  function toggleAll(checked: boolean) {
    setUnchecked(checked ? new Set() : new Set(preview.toCreate.map((p) => p.externalId)))
  }

  return (
    <Stack gap="lg">
      {/* One strip divided by lines, like the CSV import's preview. */}
      <SimpleGrid
        cols={{ base: 2, md: 4 }}
        spacing={1}
        verticalSpacing={1}
        bg="var(--z-line)"
        bd="1px solid var(--z-line)"
        style={{ ...sectionStyle, overflow: 'hidden' }}
      >
        <Figure label={t('channels.import.figures.new')} value={preview.toCreate.length} />
        <Figure label={t('channels.import.figures.toLink')} value={preview.toLink.length} />
        <Figure label={t('channels.import.figures.linked')} value={preview.linkedCount} />
        <Figure
          label={t('channels.import.figures.skipped')}
          value={preview.skipped.length}
          color={preview.skipped.length ? 'var(--z-status-low)' : undefined}
        />
      </SimpleGrid>

      {preview.toCreate.length > 0 && (
        <Section title={t('channels.import.create.title')} note={t('channels.import.create.note')}>
          <Table.ScrollContainer minWidth={640}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th w={40}>
                    <Checkbox
                      aria-label={t('channels.import.create.all')}
                      checked={allChecked}
                      indeterminate={!allChecked && toCreate.length > 0}
                      onChange={(e) => toggleAll(e.currentTarget.checked)}
                    />
                  </Table.Th>
                  <Table.Th>{t('items.columns.item')}</Table.Th>
                  <Table.Th>{t('items.columns.sku')}</Table.Th>
                  <Table.Th ta="right">{t('channels.import.create.price')}</Table.Th>
                  <Table.Th ta="right">{t('channels.import.create.stock')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {preview.toCreate.map((product) => (
                  <Table.Tr key={product.externalId}>
                    <Table.Td>
                      <Checkbox
                        aria-label={product.name}
                        checked={!unchecked.has(product.externalId)}
                        onChange={(e) => toggle(product.externalId, e.currentTarget.checked)}
                      />
                    </Table.Td>
                    <Table.Td>
                      <Text fz="sm" fw={500}>
                        {product.name}
                      </Text>
                      {product.category && (
                        <Text fz="xs" c="dimmed">
                          {product.category}
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Text ff="monospace" fz="xs" c="var(--z-text-2)">
                        {product.sku}
                      </Text>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Text ff="monospace" fz="sm" c={product.price === null ? 'dimmed' : undefined}>
                        {product.price === null ? (
                          '—'
                        ) : (
                          <>
                            {formatMoney(product.price, i18n.language)}{' '}
                            <Text component="span" fz={11} c="dimmed">
                              {t('common.currency')}
                            </Text>
                          </>
                        )}
                      </Text>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Text ff="monospace" fz="sm" c={product.stock < 0 ? 'var(--z-status-out)' : undefined}>
                        {formatNumber(product.stock, i18n.language)}{' '}
                        <Text component="span" fz={11} c="dimmed">
                          {t('items.units.kom')}
                        </Text>
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Section>
      )}

      {preview.toLink.length > 0 && (
        <Section title={t('channels.import.link.title')} note={t('channels.import.link.note')}>
          <Table.ScrollContainer minWidth={640}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t('channels.import.link.product')}</Table.Th>
                  <Table.Th>{t('items.columns.sku')}</Table.Th>
                  <Table.Th>{t('channels.import.link.item')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {preview.toLink.map((product) => (
                  <Table.Tr key={product.externalId}>
                    <Table.Td>
                      <Text fz="sm">{product.name}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text ff="monospace" fz="xs" c="var(--z-text-2)">
                        {product.sku}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Anchor component={Link} to={`/items/${product.itemId}`} fz="sm">
                        {product.itemName}
                      </Anchor>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Section>
      )}

      {preview.skipped.length > 0 && (
        <Section title={t('channels.import.skipped.title')}>
          <Table.ScrollContainer minWidth={640}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t('channels.import.link.product')}</Table.Th>
                  <Table.Th>{t('items.columns.sku')}</Table.Th>
                  <Table.Th>{t('channels.import.skipped.reason')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {preview.skipped.map((product) => (
                  <Table.Tr key={product.externalId}>
                    <Table.Td>
                      <Text fz="sm">{product.name}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text ff="monospace" fz="xs" c="var(--z-text-2)">
                        {product.sku ?? '—'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text fz="sm" c="var(--z-status-low)">
                        {t(`errors.${product.error.code}`, {
                          ...product.error.params,
                          defaultValue: t('errors.common.unexpected'),
                        })}
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Section>
      )}

      {error && (
        <Text role="alert" c="var(--mantine-color-error)" fz="sm">
          {error}
        </Text>
      )}
      {nothingToDo ? (
        <Text c="dimmed">{preview.linkedCount > 0 ? t('channels.import.allLinked') : t('channels.import.nothing')}</Text>
      ) : (
        <Group justify="flex-end" gap="md">
          <Text fz="sm" c="dimmed">
            {t('channels.import.summary', { create: toCreate.length, link: preview.toLink.length })}
          </Text>
          <Button
            onClick={() => onImport(toCreate.map((p) => p.externalId))}
            loading={importing}
            disabled={toCreate.length === 0 && preview.toLink.length === 0}
          >
            {t('channels.import.submit')}
          </Button>
        </Group>
      )}
    </Stack>
  )
}

function Figure({ label, value, color }: { label: string; value: number; color?: string }) {
  const { i18n } = useTranslation()
  return (
    <Stack gap={6} p="md" px="lg" bg="var(--z-surface)">
      <Text fz="xs" c="dimmed">
        {label}
      </Text>
      <Text ff="monospace" fz={26} fw={500} c={color} style={{ fontVariantNumeric: 'tabular-nums' }}>
        {formatNumber(value, i18n.language)}
      </Text>
    </Stack>
  )
}

function Done({ result }: { result: ProductImportResultDto }) {
  const { t } = useTranslation()
  return (
    <Section>
      <Stack gap="md" align="flex-start">
        <Title order={2}>{t('channels.import.done.title')}</Title>
        <Stack gap={4}>
          <Text>{t('channels.import.done.created', { count: result.createdCount })}</Text>
          <Text>{t('channels.import.done.linked', { count: result.linkedCount })}</Text>
          {result.skippedCount > 0 && <Text c="dimmed">{t('channels.import.done.skipped', { count: result.skippedCount })}</Text>}
        </Stack>
        <Group gap="sm">
          <Button component={Link} to="/items">
            {t('channels.import.done.toItems')}
          </Button>
          <Button component={Link} to="/channels" variant="default">
            {t('channels.import.done.toChannels')}
          </Button>
        </Group>
      </Stack>
    </Section>
  )
}
