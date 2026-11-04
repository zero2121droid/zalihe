import { ActionIcon, Anchor, Box, Button, Checkbox, Group, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core'
import { IconArrowLeft, IconX } from '@tabler/icons-react'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { ApiError, fieldErrorMessages, formErrorMessage } from '../../api/errors'
import { getListItemsQueryKey } from '../../api/generated/items/items'
import type { ItemDto } from '../../api/generated/model'
import { getListReceiptsQueryKey, useCreateReceipt } from '../../api/generated/receipts/receipts'
import { getGetStockSummaryQueryKey } from '../../api/generated/stock/stock'
import { usePermissions } from '../../auth/permissions'
import { DecimalInput } from '../../components/DecimalInput'
import { SaveButton } from '../../components/feedback'
import { ItemPicker } from '../../components/ItemPicker'
import { StepButton } from '../../components/StepButton'
import { formatDateTime, formatDecimalInput, formatMoney, formatNumber, readDecimal, todayIso } from '../../lib/format'
import { useSaved } from '../../lib/useSaved'
import { useShake } from '../../lib/useShake'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

interface Line {
  item: ItemDto
  quantity: string
  price: string
  /** Whether the price becomes the item's purchase price; chosen at first only when the item has none. */
  updatePrice: boolean
}

type LineErrors = Record<string, { quantity?: string; price?: string }>

/**
 * A receipt from a supplier with all its lines (SPEC 11.7, phase 0): supplier, document and date, then
 * items with quantity and, for those who see purchase prices, the price on the document. Posting takes
 * everything into stock at once; a price becomes the item's purchase price only where the user chooses.
 */
export function NewReceiptPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const prices = usePermissions().seePurchasePrices
  const [supplier, setSupplier] = useState('')
  const [documentNumber, setDocumentNumber] = useState('')
  const [documentDate, setDocumentDate] = useState(todayIso())
  const [lines, setLines] = useState<Line[]>([])
  const [errors, setErrors] = useState<{ supplier?: string; lines?: string; perLine: LineErrors }>({ perLine: {} })
  const saved = useSaved()
  const shake = useShake()

  const create = useCreateReceipt({
    mutation: {
      onSuccess: async (receipt) => {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getListReceiptsQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetStockSummaryQueryKey() }),
        ])
        await saved.confirm()
        void navigate(`/receipts?opened=${receipt.id}`)
      },
      onError: (error) => {
        if (!isDuplicate(error)) shake.shake()
      },
    },
  })

  const duplicate = isDuplicate(create.error) ? create.error : null
  const duplicatePostedAt = duplicate?.errors[0]?.params?.postedAt
  const serverErrors = duplicate ? {} : fieldErrorMessages(t, create.error)
  const formError = duplicate ? null : formErrorMessage(t, create.error)

  function add(item: ItemDto) {
    setErrors((current) => ({ ...current, lines: undefined }))
    create.reset()
    // The same item again adds one more to its line instead of a second line.
    setLines((current) =>
      current.some((l) => l.item.id === item.id)
        ? current.map((l) => (l.item.id === item.id ? { ...l, quantity: step(l.quantity, 1) } : l))
        : [...current, { item, quantity: '1', price: formatDecimalInput(item.purchasePrice, i18n.language, 2), updatePrice: item.purchasePrice === null }],
    )
  }

  function step(quantity: string, delta: number) {
    const typed = readDecimal(quantity)
    const base = typeof typed === 'number' ? typed : 0
    return formatDecimalInput(Math.max(0, base + delta), i18n.language)
  }

  function change(itemId: string, changes: Partial<Line>) {
    setLines((current) => current.map((l) => (l.item.id === itemId ? { ...l, ...changes } : l)))
    setErrors((current) => {
      const perLine = { ...current.perLine }
      delete perLine[itemId]
      return { ...current, perLine }
    })
    create.reset()
  }

  function submit(allowDuplicate: boolean) {
    const perLine: LineErrors = {}
    for (const line of lines) {
      const quantity = readDecimal(line.quantity)
      const price = readDecimal(line.price)
      const lineErrors: LineErrors[string] = {}
      if (quantity === null) lineErrors.quantity = t('errors.validation.required')
      else if (quantity === 'invalid') lineErrors.quantity = t('errors.validation.number')
      else if (quantity <= 0) lineErrors.quantity = t('errors.validation.positive')
      if (price === 'invalid') lineErrors.price = t('errors.validation.number')
      else if (typeof price === 'number' && price < 0) lineErrors.price = t('errors.validation.not_negative')
      if (lineErrors.quantity || lineErrors.price) perLine[line.item.id] = lineErrors
    }
    const next = {
      supplier: supplier.trim() ? undefined : t('errors.validation.required'),
      lines: lines.length === 0 ? t('receipts.form.noItems') : undefined,
      perLine,
    }
    setErrors(next)
    if (next.supplier || next.lines || Object.keys(perLine).length > 0) {
      shake.shake()
      return
    }

    create.mutate({
      data: {
        supplierName: supplier.trim(),
        documentNumber: documentNumber.trim() || null,
        documentDate,
        allowDuplicate,
        lines: lines.map((l) => {
          const price = prices ? readDecimal(l.price) : null
          const unitPrice = typeof price === 'number' ? price : null
          return {
            itemId: l.item.id,
            quantity: readDecimal(l.quantity) as number,
            unitPrice,
            updatePurchasePrice: unitPrice !== null && l.updatePrice && unitPrice !== l.item.purchasePrice,
          }
        }),
      },
    })
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    submit(false)
  }

  const total = prices
    ? lines.reduce((sum, l) => {
        const quantity = readDecimal(l.quantity)
        const price = readDecimal(l.price)
        return typeof quantity === 'number' && typeof price === 'number' ? sum + quantity * price : sum
      }, 0)
    : 0

  return (
    <Stack gap="lg">
      <Stack gap={4}>
        <Anchor component={Link} to="/receipts" fz="sm">
          <Group gap={6} component="span">
            <IconArrowLeft size={14} stroke={1.8} />
            {t('receipts.title')}
          </Group>
        </Anchor>
        <Title order={1} lts="-0.01em">
          {t('receipts.form.title')}
        </Title>
      </Stack>

      <form onSubmit={handleSubmit} noValidate className={shake.className}>
        <Stack gap="lg">
          <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="md">
            <TextInput
              label={t('receipts.form.supplier')}
              placeholder={t('receipts.form.supplierPlaceholder')}
              withAsterisk
              data-autofocus
              value={supplier}
              onChange={(e) => {
                setSupplier(e.currentTarget.value)
                setErrors((current) => ({ ...current, supplier: undefined }))
                create.reset()
              }}
              error={errors.supplier ?? serverErrors.supplierName}
            />
            <TextInput
              label={t('receipts.form.documentNumber')}
              value={documentNumber}
              onChange={(e) => {
                setDocumentNumber(e.currentTarget.value)
                create.reset()
              }}
              error={serverErrors.documentNumber}
              styles={{ input: { fontFamily: 'var(--mantine-font-family-monospace)' } }}
            />
            <TextInput
              type="date"
              label={t('receipts.form.documentDate')}
              withAsterisk
              value={documentDate}
              onChange={(e) => setDocumentDate(e.currentTarget.value)}
              error={serverErrors.documentDate}
            />
          </SimpleGrid>

          <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" p="md" style={sectionStyle}>
            <Stack gap="sm">
              <ItemPicker label={t('receipts.form.addItem')} onPick={add} error={errors.lines ?? serverErrors.lines} />
              {lines.length > 0 && (
                <Stack gap={0} role="list" aria-label={t('receipts.form.items')}>
                  {lines.map((line, index) => (
                    <ReceiptLineRow
                      key={line.item.id}
                      line={line}
                      prices={prices}
                      quantityError={errors.perLine[line.item.id]?.quantity ?? serverErrors[`lines[${index}].quantity`]}
                      priceError={errors.perLine[line.item.id]?.price ?? serverErrors[`lines[${index}].unitPrice`]}
                      onChange={(changes) => change(line.item.id, changes)}
                      onStep={(delta) => change(line.item.id, { quantity: step(line.quantity, delta) })}
                      onRemove={() => setLines((current) => current.filter((l) => l.item.id !== line.item.id))}
                    />
                  ))}
                </Stack>
              )}
              {lines.length > 0 && (
                <Group justify="space-between" pt="xs">
                  <Text fz="sm" c="dimmed">
                    {t('receipts.form.lineCount', { count: lines.length })}
                  </Text>
                  {prices && total > 0 && (
                    <Text fz="sm">
                      <Text component="span" c="dimmed" fz="sm">
                        {t('receipts.form.total')}{' '}
                      </Text>
                      <Text component="span" ff="monospace" fw={500}>
                        {formatMoney(total, i18n.language)}
                      </Text>{' '}
                      <Text component="span" fz="xs" c="dimmed">
                        {t('common.currency')}
                      </Text>
                    </Text>
                  )}
                </Group>
              )}
            </Stack>
          </Box>

          {duplicate && (
            <Stack gap="sm" p="md" bg="var(--z-surface-2)" style={sectionStyle} align="flex-start" role="alert">
              <Text fz="sm">
                {t('receipts.form.duplicate', {
                  date: typeof duplicatePostedAt === 'string' ? formatDateTime(duplicatePostedAt, i18n.language) : '',
                })}
              </Text>
              <Button variant="default" onClick={() => submit(true)} loading={create.isPending}>
                {t('receipts.form.postAnyway')}
              </Button>
            </Stack>
          )}

          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}

          <Group justify="space-between" gap="sm" className="z-modal-actions">
            <Text fz="xs" c="dimmed">
              {t('receipts.form.postedHint')}
            </Text>
            <Group gap="sm">
              <Button variant="default" className="z-modal-cancel" component={Link} to="/receipts">
                {t('common.cancel')}
              </Button>
              <SaveButton type="submit" loading={create.isPending && !duplicate} saved={saved.saved}>
                {t('receipts.form.submit')}
              </SaveButton>
            </Group>
          </Group>
        </Stack>
      </form>
    </Stack>
  )
}

function isDuplicate(error: unknown): error is ApiError {
  return error instanceof ApiError && error.code === 'receipt.duplicate_document'
}

interface ReceiptLineRowProps {
  line: Line
  prices: boolean
  quantityError: string | undefined
  priceError: string | undefined
  onChange: (changes: Partial<Line>) => void
  onStep: (delta: number) => void
  onRemove: () => void
}

function ReceiptLineRow({ line, prices, quantityError, priceError, onChange, onStep, onRemove }: ReceiptLineRowProps) {
  const { t, i18n } = useTranslation()
  const { item } = line
  const unit = t(`items.units.${item.unit}`)
  const price = readDecimal(line.price)
  // The choice matters only when the document's price differs from the item's.
  const priceDiffers = typeof price === 'number' && price !== item.purchasePrice

  return (
    <Group role="listitem" gap="sm" py="sm" wrap="wrap" align="flex-start" style={{ borderBottom: '1px solid var(--z-line-soft)' }}>
      <Stack gap={0} miw={0} flex="1 1 220px">
        <Text fz="sm" fw={500} truncate>
          {item.name}
        </Text>
        <Text ff="monospace" fz="xs" c="dimmed">
          {item.sku} · {formatNumber(item.stock, i18n.language)} {unit} {t('receipts.form.inStock')}
        </Text>
      </Stack>
      <Group gap="xs" wrap="nowrap" align="flex-start">
        <StepButton label={t('stock.form.decrease')} sign="−" onStep={() => onStep(-1)} />
        <DecimalInput
          w={110}
          aria-label={t('receipts.form.quantity', { name: item.name })}
          rightSection={
            <Text fz="xs" c="dimmed" pr="xs">
              {unit}
            </Text>
          }
          value={line.quantity}
          onChange={(quantity) => onChange({ quantity })}
          error={quantityError}
        />
        <StepButton label={t('stock.form.increase')} sign="+" onStep={() => onStep(1)} />
      </Group>
      {prices && (
        <Stack gap={6} w={200}>
          <DecimalInput
            aria-label={t('receipts.form.unitPrice', { name: item.name })}
            placeholder={t('receipts.form.pricePlaceholder')}
            rightSection={
              <Text fz="xs" c="dimmed" pr="xs">
                {t('common.currency')}
              </Text>
            }
            rightSectionWidth={48}
            value={line.price}
            onChange={(next) => onChange({ price: next })}
            error={priceError}
          />
          {priceDiffers && (
            <Checkbox
              size="xs"
              checked={line.updatePrice}
              onChange={(e) => onChange({ updatePrice: e.currentTarget.checked })}
              label={t('receipts.form.updatePrice')}
              description={
                item.purchasePrice === null
                  ? t('receipts.form.noCurrentPrice')
                  : t('receipts.form.currentPrice', { price: `${formatMoney(item.purchasePrice, i18n.language)} ${t('common.currency')}` })
              }
            />
          )}
        </Stack>
      )}
      <ActionIcon
        variant="subtle"
        color="gray"
        size="var(--z-icon-button)"
        aria-label={t('receipts.form.remove', { name: item.name })}
        onClick={onRemove}
      >
        <IconX size={16} stroke={1.8} />
      </ActionIcon>
    </Group>
  )
}
