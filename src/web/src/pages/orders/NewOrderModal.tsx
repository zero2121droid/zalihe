import { ActionIcon, Button, Group, Loader, Modal, Select, Stack, Text, TextInput } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconSearch, IconX } from '@tabler/icons-react'
import { keepPreviousData, useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import { getListItemsQueryKey, useListItems } from '../../api/generated/items/items'
import type { ItemDto } from '../../api/generated/model'
import { getListOrdersQueryKey, useCreateOrder } from '../../api/generated/orders/orders'
import { getGetStockSummaryQueryKey } from '../../api/generated/stock/stock'
import { DecimalInput } from '../../components/DecimalInput'
import { SaveButton } from '../../components/feedback'
import { StepButton } from '../../components/StepButton'
import { formatDecimalInput, formatNumber, readDecimal } from '../../lib/format'
import { useSaved } from '../../lib/useSaved'
import { useShake } from '../../lib/useShake'

/** Items offered while searching; typing more narrows them. */
const RESULTS = 8

interface Line {
  item: ItemDto
  quantity: string
}

/**
 * A manual order for a sale outside the shop (SPEC 11.5): items found by name, SKU or barcode, a
 * quantity for each, and an optional reference and note. The API numbers it (R-0001…) and takes the
 * items from stock; stock may go below zero, so the form only warns.
 */
export function NewOrderModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [lines, setLines] = useState<Line[]>([])
  const [reference, setReference] = useState('')
  const [note, setNote] = useState('')
  const [linesError, setLinesError] = useState<string | null>(null)
  const [quantityErrors, setQuantityErrors] = useState<Record<string, string>>({})
  const [searchText, setSearchText] = useState('')
  const [search] = useDebouncedValue(searchText.trim(), 150)
  const saved = useSaved()
  const shake = useShake()

  const items = useListItems(
    { search, pageSize: RESULTS },
    { query: { enabled: opened && search !== '', placeholderData: keepPreviousData } },
  )
  const results = search ? (items.data?.items ?? []) : []

  const create = useCreateOrder({
    mutation: {
      onSuccess: async () => {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getListOrdersQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetStockSummaryQueryKey() }),
        ])
        await saved.confirm()
        close()
      },
      onError: shake.shake,
    },
  })

  function close() {
    setLines([])
    setReference('')
    setNote('')
    setLinesError(null)
    setQuantityErrors({})
    setSearchText('')
    create.reset()
    onClose()
  }

  function add(itemId: string | null) {
    const item = results.find((i) => i.id === itemId)
    setSearchText('')
    if (!item) return
    setLinesError(null)
    // The same item again adds one more to its line instead of a second line.
    setLines((current) =>
      current.some((l) => l.item.id === item.id)
        ? current.map((l) => (l.item.id === item.id ? { ...l, quantity: step(l.quantity, 1) } : l))
        : [...current, { item, quantity: '1' }],
    )
  }

  function step(quantity: string, delta: number) {
    const typed = readDecimal(quantity)
    const base = typeof typed === 'number' ? typed : 0
    return formatDecimalInput(Math.max(0, base + delta), i18n.language)
  }

  function setQuantity(itemId: string, quantity: string) {
    setLines((current) => current.map((l) => (l.item.id === itemId ? { ...l, quantity } : l)))
    setQuantityErrors((current) => {
      const next = { ...current }
      delete next[itemId]
      return next
    })
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (lines.length === 0) {
      setLinesError(t('orders.form.noItems'))
      shake.shake()
      return
    }

    const errors: Record<string, string> = {}
    for (const line of lines) {
      const value = readDecimal(line.quantity)
      if (value === null) errors[line.item.id] = t('errors.validation.required')
      else if (value === 'invalid') errors[line.item.id] = t('errors.validation.number')
      else if (value <= 0) errors[line.item.id] = t('errors.validation.positive')
    }
    setQuantityErrors(errors)
    if (Object.keys(errors).length > 0) {
      shake.shake()
      return
    }

    create.mutate({
      data: {
        lines: lines.map((l) => ({ itemId: l.item.id, quantity: readDecimal(l.quantity) as number })),
        reference: reference.trim() || null,
        note: note.trim() || null,
      },
    })
  }

  const serverErrors = fieldErrorMessages(t, create.error)
  const formError = formErrorMessage(t, create.error)
  const goesNegative = lines.some((l) => {
    const value = readDecimal(l.quantity)
    return typeof value === 'number' && l.item.stock - value < 0
  })

  return (
    <Modal opened={opened} onClose={close} title={t('orders.form.title')}>
      <form onSubmit={handleSubmit} noValidate className={shake.className}>
        <Stack gap="md">
          <Select
            label={t('orders.form.addItem')}
            placeholder={t('orders.form.addItemPlaceholder')}
            leftSection={<IconSearch size={16} stroke={1.8} />}
            rightSection={items.isFetching ? <Loader size={16} /> : null}
            searchable
            searchValue={searchText}
            onSearchChange={setSearchText}
            value={null}
            onChange={add}
            // The API already searched by name, SKU and barcode; the field must not filter again.
            filter={({ options }) => options}
            data={results.map((i) => ({ value: i.id, label: i.name }))}
            renderOption={({ option }) => {
              const item = results.find((i) => i.id === option.value)!
              return (
                <Group justify="space-between" gap="sm" wrap="nowrap" w="100%">
                  <Stack gap={0} miw={0}>
                    <Text fz="sm" truncate>
                      {item.name}
                    </Text>
                    <Text ff="monospace" fz="xs" c="dimmed">
                      {item.sku}
                    </Text>
                  </Stack>
                  <Text ff="monospace" fz="sm" style={{ whiteSpace: 'nowrap' }}>
                    {formatNumber(item.stock, i18n.language)}{' '}
                    <Text component="span" fz="xs" c="dimmed">
                      {t(`items.units.${item.unit}`)}
                    </Text>
                  </Text>
                </Group>
              )
            }}
            nothingFoundMessage={search && items.isSuccess ? t('orders.form.noMatches', { search }) : undefined}
            error={linesError ?? serverErrors.lines}
            data-autofocus
          />

          {lines.length > 0 && (
            <Stack gap={0} aria-label={t('orders.form.items')} role="list">
              {lines.map((line, index) => (
                <OrderLineRow
                  key={line.item.id}
                  line={line}
                  error={quantityErrors[line.item.id] ?? serverErrors[`lines[${index}].quantity`]}
                  onQuantity={(quantity) => setQuantity(line.item.id, quantity)}
                  onStep={(delta) => setQuantity(line.item.id, step(line.quantity, delta))}
                  onRemove={() => setLines((current) => current.filter((l) => l.item.id !== line.item.id))}
                />
              ))}
            </Stack>
          )}

          {goesNegative && (
            <Text fz="sm" c="var(--z-status-out)">
              {t('orders.form.negativeWarning')}
            </Text>
          )}

          <Group grow align="flex-start" className="z-phone-stack">
            <TextInput
              label={t('orders.form.reference')}
              placeholder={t('orders.form.referencePlaceholder')}
              value={reference}
              onChange={(e) => setReference(e.currentTarget.value)}
              error={serverErrors.reference}
            />
            <TextInput
              label={t('orders.form.note')}
              description={t('orders.form.noteHint')}
              inputWrapperOrder={['label', 'input', 'description', 'error']}
              value={note}
              onChange={(e) => setNote(e.currentTarget.value)}
              error={serverErrors.note}
            />
          </Group>

          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}

          <Group justify="flex-end" gap="sm" className="z-modal-actions">
            <Button variant="default" className="z-modal-cancel" onClick={close}>
              {t('common.cancel')}
            </Button>
            <SaveButton type="submit" loading={create.isPending} saved={saved.saved}>
              {t('orders.form.submit')}
            </SaveButton>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}

interface OrderLineRowProps {
  line: Line
  error: string | undefined
  onQuantity: (quantity: string) => void
  onStep: (delta: number) => void
  onRemove: () => void
}

function OrderLineRow({ line, error, onQuantity, onStep, onRemove }: OrderLineRowProps) {
  const { t, i18n } = useTranslation()
  const { item } = line
  const unit = t(`items.units.${item.unit}`)

  return (
    <Group role="listitem" gap="sm" py="sm" wrap="wrap" align="flex-start" style={{ borderBottom: '1px solid var(--z-line-soft)' }}>
      <Stack gap={0} miw={0} flex="1 1 200px">
        <Text fz="sm" fw={500} truncate>
          {item.name}
        </Text>
        <Text ff="monospace" fz="xs" c="dimmed">
          {item.sku} · {formatNumber(item.stock, i18n.language)} {unit} {t('orders.form.inStock')}
        </Text>
      </Stack>
      <Group gap="xs" wrap="nowrap" align="flex-start">
        <StepButton label={t('stock.form.decrease')} sign="−" onStep={() => onStep(-1)} />
        <DecimalInput
          w={110}
          aria-label={t('orders.form.quantity', { name: item.name })}
          rightSection={
            <Text fz="xs" c="dimmed" pr="xs">
              {unit}
            </Text>
          }
          value={line.quantity}
          onChange={onQuantity}
          error={error}
        />
        <StepButton label={t('stock.form.increase')} sign="+" onStep={() => onStep(1)} />
        <ActionIcon
          variant="subtle"
          color="gray"
          size="var(--z-icon-button)"
          aria-label={t('orders.form.remove', { name: item.name })}
          onClick={onRemove}
        >
          <IconX size={16} stroke={1.8} />
        </ActionIcon>
      </Group>
    </Group>
  )
}
