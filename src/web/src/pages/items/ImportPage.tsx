import {
  Anchor,
  Box,
  Button,
  FileInput,
  Group,
  Loader,
  Select,
  SimpleGrid,
  Stack,
  Stepper,
  Table,
  Text,
  Title,
} from '@mantine/core'
import { IconArrowLeft, IconFileTypeCsv } from '@tabler/icons-react'
import { useQueryClient } from '@tanstack/react-query'
import type { TFunction } from 'i18next'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { type ApiErrorItem, fieldErrorMessages, formErrorMessage } from '../../api/errors'
import { useAnalyzeItemImport, useImportItems, usePreviewItemImport } from '../../api/generated/imports/imports'
import { getListItemsQueryKey } from '../../api/generated/items/items'
import {
  type ImportAnalysisDto,
  type ImportPreviewDto,
  type ImportResultDto,
  type PreviewItemImportBody,
  Unit,
} from '../../api/generated/model'
import { getGetStockSummaryQueryKey } from '../../api/generated/stock/stock'
import { formatNumber } from '../../lib/format'

// Mirrors ItemImportField on the server. OpenAPI only sees it as dictionary keys, so it isn't generated.
const fields = [
  'name',
  'sku',
  'barcode',
  'unit',
  'category',
  'groupName',
  'purchasePrice',
  'salePrice',
  'minStock',
  'initialStock',
] as const
type ItemImportField = (typeof fields)[number]
type Mapping = Partial<Record<ItemImportField, number>>

const requiredFields: ItemImportField[] = ['name', 'sku']
const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

/** Form field names of the API for each field, e.g. "name" → "NameColumn". */
function toBody(file: File, mapping: Mapping, defaultUnit: Unit): PreviewItemImportBody {
  const body: PreviewItemImportBody = { File: file, DefaultUnit: defaultUnit }
  for (const [field, column] of Object.entries(mapping)) {
    const key = `${field[0].toUpperCase()}${field.slice(1)}Column` as keyof PreviewItemImportBody
    ;(body as Record<string, unknown>)[key] = column
  }
  return body
}

function fieldLabel(t: TFunction, field: string) {
  return field === 'initialStock' ? t('items.import.fields.initialStock') : t(`items.form.${field}`)
}

/** "Jedinica mere: Nepoznata jedinica mere „kutija”." */
function issueText(t: TFunction, error: ApiErrorItem) {
  const message = t(`errors.${error.code}`, { ...error.params, defaultValue: t('errors.common.unexpected') })
  return error.field && error.code !== 'import.sku_exists' ? `${fieldLabel(t, error.field)}: ${message}` : message
}

/** Spreadsheet column letter for a 0-based index: 0 → A, 26 → AA. */
function columnLetter(index: number): string {
  return index < 26 ? String.fromCharCode(65 + index) : columnLetter(Math.floor(index / 26) - 1) + columnLetter(index % 26)
}

export function ImportPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [step, setStep] = useState(0)
  const [file, setFile] = useState<File | null>(null)
  const [analysis, setAnalysis] = useState<ImportAnalysisDto | null>(null)
  const [mapping, setMapping] = useState<Mapping>({})
  const [defaultUnit, setDefaultUnit] = useState<Unit>(Unit.kom)
  const [preview, setPreview] = useState<ImportPreviewDto | null>(null)
  const [result, setResult] = useState<ImportResultDto | null>(null)

  const analyze = useAnalyzeItemImport({
    mutation: {
      onSuccess: (data) => {
        setAnalysis(data)
        setMapping(data.suggestedMapping as Mapping)
        setStep(1)
      },
    },
  })
  const check = usePreviewItemImport({
    mutation: {
      onSuccess: (data) => {
        setPreview(data)
        setStep(2)
      },
    },
  })
  const runImport = useImportItems({
    mutation: {
      onSuccess: async (data) => {
        setResult(data)
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetStockSummaryQueryKey() }),
        ])
      },
    },
  })

  function chooseFile(next: File | null) {
    setFile(next)
    setAnalysis(null)
    setPreview(null)
    analyze.reset()
    if (next) analyze.mutate({ data: { File: next } })
  }

  function restart() {
    setStep(0)
    setFile(null)
    setAnalysis(null)
    setPreview(null)
    setResult(null)
    for (const mutation of [analyze, check, runImport]) mutation.reset()
  }

  const fileError = fieldErrorMessages(t, analyze.error ?? check.error ?? runImport.error).file

  return (
    <Stack gap="lg">
      <Stack gap="xs">
        <Anchor component={Link} to="/items" fz="sm">
          <Group gap={6} component="span">
            <IconArrowLeft size={14} stroke={1.8} />
            {t('items.title')}
          </Group>
        </Anchor>
        <Title order={1} lts="-0.01em">
          {t('items.import.title')}
        </Title>
      </Stack>

      {result ? (
        <Done result={result} onRestart={restart} />
      ) : (
        <Stepper active={step} onStepClick={(next) => next < step && setStep(next)} size="sm">
          <Stepper.Step label={t('items.import.steps.file')}>
            <Section>
              <Stack gap="md">
                <FileInput
                  label={t('items.import.file.label')}
                  placeholder={t('items.import.file.placeholder')}
                  accept=".csv,text/csv"
                  leftSection={<IconFileTypeCsv size={16} stroke={1.8} />}
                  value={file}
                  onChange={chooseFile}
                  error={fileError ?? (analyze.error && !fileError ? formErrorMessage(t, analyze.error) : undefined)}
                  clearable
                />
                {analyze.isPending && (
                  <Group gap="xs">
                    <Loader size="xs" />
                    <Text fz="sm" c="dimmed">
                      {t('items.import.file.analyzing')}
                    </Text>
                  </Group>
                )}
                <Text fz="sm" c="dimmed">
                  {t('items.import.file.help')} {t('items.import.file.recognized')}
                </Text>
                <SampleFileLink />
              </Stack>
            </Section>
          </Stepper.Step>

          <Stepper.Step label={t('items.import.steps.columns')}>
            {analysis && (
              <ColumnsStep
                analysis={analysis}
                mapping={mapping}
                onMappingChange={setMapping}
                defaultUnit={defaultUnit}
                onDefaultUnitChange={setDefaultUnit}
                error={check.error ? (fileError ?? formErrorMessage(t, check.error)) : null}
                checking={check.isPending}
                onBack={() => setStep(0)}
                onCheck={() => file && check.mutate({ data: toBody(file, mapping, defaultUnit) })}
              />
            )}
          </Stepper.Step>

          <Stepper.Step label={t('items.import.steps.preview')}>
            {preview && (
              <PreviewStep
                preview={preview}
                error={runImport.error ? (fileError ?? formErrorMessage(t, runImport.error)) : null}
                importing={runImport.isPending}
                onBack={() => setStep(1)}
                onImport={() => file && runImport.mutate({ data: toBody(file, mapping, defaultUnit) })}
              />
            )}
          </Stepper.Step>
        </Stepper>
      )}
    </Stack>
  )
}

function Section({ children }: { children: React.ReactNode }) {
  return (
    <Box component="section" p="lg" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      {children}
    </Box>
  )
}

/** A small CSV in the user's language, made in the browser, showing the expected layout. */
function SampleFileLink() {
  const { t } = useTranslation()
  function download() {
    const header = ['name', 'sku', 'unit', 'purchasePrice', 'minStock', 'initialStock'].map((f) => fieldLabel(t, f))
    const rows = [
      ['Kafa Etiopija 250 g', 'KF-ETI-250', t('items.units.kom'), '900,50', '10', '24'],
      ['Espresso mešavina', 'KF-ESP-1000', t('items.units.kg'), '2000', '5', '12,5'],
    ]
    const csv = '﻿' + [header, ...rows].map((r) => r.join(';')).join('\r\n')
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
    const link = Object.assign(document.createElement('a'), { href: url, download: 'artikli-primer.csv' })
    link.click()
    URL.revokeObjectURL(url)
  }
  return (
    <Anchor component="button" type="button" fz="sm" onClick={download} style={{ alignSelf: 'flex-start' }}>
      {t('items.import.file.sample')}
    </Anchor>
  )
}

interface ColumnsStepProps {
  analysis: ImportAnalysisDto
  mapping: Mapping
  onMappingChange: (mapping: Mapping) => void
  defaultUnit: Unit
  onDefaultUnitChange: (unit: Unit) => void
  error: string | null
  checking: boolean
  onBack: () => void
  onCheck: () => void
}

function ColumnsStep(props: ColumnsStepProps) {
  const { analysis, mapping, onMappingChange, defaultUnit, onDefaultUnitChange, error, checking, onBack, onCheck } = props
  const { t, i18n } = useTranslation()
  const columnOptions = [
    { value: '', label: t('items.import.columns.skip') },
    ...analysis.columns.map((name, i) => ({ value: String(i), label: `${columnLetter(i)} · ${name || '—'}` })),
  ]
  const canCheck = requiredFields.every((f) => mapping[f] !== undefined)

  return (
    <Stack gap="lg">
      <Section>
        <Stack gap="md">
          <Text c="dimmed">{t('items.import.columns.intro')}</Text>
          <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="md" verticalSpacing="sm">
            {fields.map((field) => (
              <Select
                key={field}
                label={fieldLabel(t, field)}
                withAsterisk={requiredFields.includes(field)}
                data={columnOptions}
                value={mapping[field] === undefined ? '' : String(mapping[field])}
                onChange={(value) => {
                  const next = { ...mapping }
                  if (value) next[field] = Number(value)
                  else delete next[field]
                  onMappingChange(next)
                }}
                allowDeselect={false}
              />
            ))}
            <Select
              label={t('items.import.columns.defaultUnit')}
              data={Object.values(Unit).map((u) => ({ value: u, label: t(`items.units.${u}`) }))}
              value={defaultUnit}
              onChange={(value) => value && onDefaultUnitChange(value as Unit)}
              allowDeselect={false}
            />
          </SimpleGrid>
        </Stack>
      </Section>

      <Section>
        <Stack gap="sm">
          <Text fz="sm" c="dimmed">
            {t('items.import.columns.rows', { count: analysis.rowCount, formattedCount: formatNumber(analysis.rowCount, i18n.language) })}
          </Text>
          <Table.ScrollContainer minWidth={600}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  {analysis.columns.map((name, i) => (
                    <Table.Th key={i}>{`${columnLetter(i)} · ${name}`}</Table.Th>
                  ))}
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {analysis.sampleRows.map((row, r) => (
                  <Table.Tr key={r}>
                    {row.map((cell, c) => (
                      <Table.Td key={c}>
                        <Text fz="sm" c="var(--z-text-2)">
                          {cell}
                        </Text>
                      </Table.Td>
                    ))}
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Stack>
      </Section>

      {error && (
        <Text role="alert" c="var(--mantine-color-error)" fz="sm">
          {error}
        </Text>
      )}
      <Group justify="space-between">
        <Button variant="default" onClick={onBack}>
          {t('items.import.back')}
        </Button>
        <Button onClick={onCheck} disabled={!canCheck} loading={checking}>
          {t('items.import.columns.check')}
        </Button>
      </Group>
    </Stack>
  )
}

interface PreviewStepProps {
  preview: ImportPreviewDto
  error: string | null
  importing: boolean
  onBack: () => void
  onImport: () => void
}

function PreviewStep({ preview, error, importing, onBack, onImport }: PreviewStepProps) {
  const { t, i18n } = useTranslation()

  return (
    <Stack gap="lg">
      {/* One strip divided by lines, like the figures on the item page. */}
      <SimpleGrid cols={3} spacing={1} bg="var(--z-line)" bd="1px solid var(--z-line)" style={{ ...sectionStyle, overflow: 'hidden' }}>
        <Figure label={t('items.import.preview.ready')} value={preview.readyCount} />
        <Figure label={t('items.import.preview.errors')} value={preview.errorCount} color={preview.errorCount ? 'var(--z-status-out)' : undefined} />
        <Figure label={t('items.import.preview.skipped')} value={preview.skippedCount} color={preview.skippedCount ? 'var(--z-status-low)' : undefined} />
      </SimpleGrid>

      {preview.issues.length > 0 && (
        <Section>
          <Stack gap="sm">
            <Title order={2}>{t('items.import.preview.issuesTitle')}</Title>
            {preview.skippedCount > 0 && (
              <Text fz="sm" c="dimmed">
                {t('items.import.preview.skippedNote')}
              </Text>
            )}
            <Table.ScrollContainer minWidth={600}>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th ta="right">{t('items.import.preview.row')}</Table.Th>
                    <Table.Th>{t('items.columns.sku')}</Table.Th>
                    <Table.Th>{t('items.columns.item')}</Table.Th>
                    <Table.Th>{t('items.import.preview.problem')}</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {preview.issues.map((issue) => (
                    <Table.Tr key={issue.rowNumber}>
                      <Table.Td ta="right">
                        <Text ff="monospace" fz="sm" c="dimmed">
                          {issue.rowNumber}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text ff="monospace" fz="xs" c="var(--z-text-2)">
                          {issue.sku}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text fz="sm">{issue.name}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Stack gap={2}>
                          {issue.errors.map((e, i) => (
                            <Text key={i} fz="sm" c={issue.skipped ? 'var(--z-status-low)' : 'var(--z-status-out)'}>
                              {issueText(t, e)}
                            </Text>
                          ))}
                        </Stack>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Stack>
        </Section>
      )}

      {preview.sample.length > 0 && (
        <Section>
          <Stack gap="sm">
            <Title order={2}>{t('items.import.preview.sampleTitle')}</Title>
            <Table>
              <Table.Tbody>
                {preview.sample.map((item) => (
                  <Table.Tr key={item.rowNumber}>
                    <Table.Td>
                      <Text fz="sm" fw={500}>
                        {item.name}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text ff="monospace" fz="xs" c="var(--z-text-2)">
                        {item.sku}
                      </Text>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Text ff="monospace" fz="sm">
                        {formatNumber(item.initialStock, i18n.language)}{' '}
                        <Text component="span" fz={11} c="dimmed">
                          {t(`items.units.${item.unit}`)}
                        </Text>
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Stack>
        </Section>
      )}

      {error && (
        <Text role="alert" c="var(--mantine-color-error)" fz="sm">
          {error}
        </Text>
      )}
      <Group justify="space-between">
        <Button variant="default" onClick={onBack}>
          {t('items.import.back')}
        </Button>
        {preview.readyCount > 0 ? (
          <Button onClick={onImport} loading={importing}>
            {t('items.import.preview.submit', { count: preview.readyCount })}
          </Button>
        ) : (
          <Text c="dimmed" fz="sm">
            {t('items.import.preview.nothing')}
          </Text>
        )}
      </Group>
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

function Done({ result, onRestart }: { result: ImportResultDto; onRestart: () => void }) {
  const { t } = useTranslation()
  const skipped = result.errorCount + result.skippedCount
  return (
    <Section>
      <Stack gap="md" align="flex-start">
        <Title order={2}>{t('items.import.done.title', { count: result.importedCount })}</Title>
        {skipped > 0 && <Text c="dimmed">{t('items.import.done.skipped', { count: skipped })}</Text>}
        <Group gap="sm">
          <Button component={Link} to="/items">
            {t('items.import.done.toItems')}
          </Button>
          <Button variant="default" onClick={onRestart}>
            {t('items.import.done.again')}
          </Button>
        </Group>
      </Stack>
    </Section>
  )
}
