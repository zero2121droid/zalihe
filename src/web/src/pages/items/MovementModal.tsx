import { Button, Group, Modal, Stack, Text, TextInput } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import {
  getGetItemHistoryQueryKey,
  getGetItemQueryKey,
  getGetItemShopStockQueryKey,
  getGetItemStockHistoryQueryKey,
  getListItemsQueryKey,
} from '../../api/generated/items/items'
import type { ItemDto, ManualMovementKind } from '../../api/generated/model'
import { getListOrdersQueryKey, useCreateOrder } from '../../api/generated/orders/orders'
import { getGetStockSummaryQueryKey, useRecordStockMovement } from '../../api/generated/stock/stock'
import { DecimalInput } from '../../components/DecimalInput'
import { SaveButton } from '../../components/feedback'
import { Odometer } from '../../components/Odometer'
import { StepButton } from '../../components/StepButton'
import { formatDecimalInput, formatNumber, formatSignedQuantity, readDecimal } from '../../lib/format'
import { useSaved } from '../../lib/useSaved'
import { useShake } from '../../lib/useShake'
import classes from './MovementModal.module.css'

interface MovementModalProps {
  item: ItemDto
  /** Which movement to record; null keeps the modal closed. */
  kind: ManualMovementKind | null
  onClose: () => void
}

/**
 * Records a receipt, sale, return or correction for one item. A sale is saved as a manual order with
 * this one item (SPEC 11.5), so every sale is also in Porudžbine and is undone by cancelling it. The
 * preview below the quantity only helps the user; the API computes the actual change from the stock
 * at that moment.
 */
export function MovementModal({ item, kind, onClose }: MovementModalProps) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [quantity, setQuantity] = useState('')
  const [note, setNote] = useState('')
  const [reference, setReference] = useState('')
  const [quantityError, setQuantityError] = useState<string | null>(null)
  const saved = useSaved()
  const shake = useShake()

  async function onSaved() {
    await Promise.all([
          queryClient.invalidateQueries({ queryKey: getGetItemQueryKey(item.id) }),
          queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetItemHistoryQueryKey(item.id) }),
          queryClient.invalidateQueries({ queryKey: getGetItemShopStockQueryKey(item.id) }),
          queryClient.invalidateQueries({ queryKey: getGetItemStockHistoryQueryKey(item.id) }),
          queryClient.invalidateQueries({ queryKey: getGetStockSummaryQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getListOrdersQueryKey() }),
        ])
    await saved.confirm()
    close()
  }

  const record = useRecordStockMovement({ mutation: { onSuccess: onSaved, onError: shake.shake } })
  const sell = useCreateOrder({ mutation: { onSuccess: onSaved, onError: shake.shake } })
  const save = kind === 'sale' ? sell : record

  function close() {
    setQuantity('')
    setNote('')
    setReference('')
    setQuantityError(null)
    record.reset()
    sell.reset()
    onClose()
  }

  const errors = fieldErrorMessages(t, save.error)
  // The order's single line reports its quantity as "lines[0].quantity".
  const serverErrors: Record<string, string | undefined> = { ...errors, quantity: errors.quantity ?? errors['lines[0].quantity'] }
  const formError = formErrorMessage(t, save.error)
  const unit = t(`items.units.${item.unit}`)
  const isCount = kind === 'count'

  const value = readDecimal(quantity)
  const amount = typeof value === 'number' ? value : null
  const change =
    amount === null ? null : kind === 'sale' ? -amount : kind === 'count' ? amount - item.stock : amount
  const after = change === null ? null : item.stock + change

  // − and + change the typed quantity by one (never below zero); an empty field counts as zero.
  function stepBy(delta: number) {
    setQuantity((current) => {
      const typed = readDecimal(current)
      const base = typeof typed === 'number' ? typed : 0
      return formatDecimalInput(Math.max(0, base + delta), i18n.language)
    })
    setQuantityError(null)
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!kind) return
    if (value === 'invalid' || value === null) {
      setQuantityError(value === null ? t('errors.validation.required') : t('errors.validation.number'))
      shake.shake()
      return
    }
    if (kind === 'sale') {
      sell.mutate({
        data: { lines: [{ itemId: item.id, quantity: value }], reference: reference.trim() || null, note: note.trim() || null },
      })
    } else {
      record.mutate({ itemId: item.id, data: { kind, quantity: value, note: note.trim() || null } })
    }
  }

  return (
    <Modal opened={kind !== null} onClose={close} title={kind ? t(`stock.form.titles.${kind}`) : ''}>
      <form onSubmit={handleSubmit} noValidate className={shake.className}>
        <Stack gap="md">
          <Group justify="space-between" align="baseline">
            <Stack gap={0} miw={0}>
              <Text fw={600} truncate>
                {item.name}
              </Text>
              <Text ff="monospace" fz="xs" c="dimmed">
                {item.sku}
              </Text>
            </Stack>
            <Text fz="sm" c="dimmed">
              {t('stock.form.current')}:{' '}
              <Text component="span" ff="monospace" c="var(--z-text)">
                {formatNumber(item.stock, i18n.language)} {unit}
              </Text>
            </Text>
          </Group>

          <Group gap="xs" align="flex-start" wrap="nowrap" className={classes.row}>
            <DecimalInput
              flex={1}
              classNames={{ input: classes.quantity }}
              label={kind ? t(`stock.form.quantity.${kind}`) : ''}
              description={isCount ? t('stock.form.countHint') : undefined}
              inputWrapperOrder={['label', 'input', 'description', 'error']}
              withAsterisk
              data-autofocus
              rightSection={
                <Text fz="xs" c="dimmed" pr="xs">
                  {unit}
                </Text>
              }
              value={quantity}
              onChange={(next) => {
                setQuantity(next)
                setQuantityError(null)
              }}
              error={quantityError ?? serverErrors.quantity}
            />
            <Group gap="xs" wrap="nowrap" mt={kind ? 26 : 0} className={classes.steps}>
              <StepButton label={t('stock.form.decrease')} sign="−" onStep={() => stepBy(-1)} />
              <StepButton label={t('stock.form.increase')} sign="+" onStep={() => stepBy(1)} />
            </Group>
          </Group>

          {change !== null && after !== null && (
            <Stack gap={4}>
              <Group justify="space-between" fz="sm">
                <Text fz="sm" c="dimmed">
                  {isCount ? t('stock.form.difference') : t('stock.form.after')}
                </Text>
                <Text ff="monospace" fz="sm" c={after < 0 ? 'var(--z-status-out)' : undefined}>
                  <Odometer text={isCount ? formatSignedQuantity(change, i18n.language) : formatNumber(after, i18n.language)} />{' '}
                  {unit}
                </Text>
              </Group>
              {after < 0 && (
                <Text fz="sm" c="var(--z-status-out)">
                  {t('stock.form.negativeWarning')}
                </Text>
              )}
            </Stack>
          )}

          {kind === 'sale' && (
            <TextInput
              label={t('orders.form.reference')}
              placeholder={t('orders.form.referencePlaceholder')}
              value={reference}
              onChange={(e) => setReference(e.currentTarget.value)}
              error={serverErrors.reference}
            />
          )}

          <TextInput
            label={isCount ? t('stock.form.noteRequired') : t('stock.form.note')}
            description={kind === 'sale' ? t('orders.form.noteHint') : undefined}
            inputWrapperOrder={['label', 'input', 'description', 'error']}
            withAsterisk={isCount}
            value={note}
            onChange={(e) => setNote(e.currentTarget.value)}
            error={serverErrors.note}
          />

          {kind === 'sale' && (
            <Text fz="xs" c="dimmed">
              {t('stock.form.saleHint')}
            </Text>
          )}

          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}

          <Group justify="flex-end" gap="sm" className="z-modal-actions">
            <Button variant="default" className="z-modal-cancel" onClick={close}>
              {t('common.cancel')}
            </Button>
            <SaveButton type="submit" loading={save.isPending} saved={saved.saved}>
              {t('stock.form.submit')}
            </SaveButton>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
