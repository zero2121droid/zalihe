import { Button, Group, Modal, Stack, Text, TextInput } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import { getGetItemHistoryQueryKey, getGetItemQueryKey, getListItemsQueryKey } from '../../api/generated/items/items'
import type { ItemDto, ManualMovementKind } from '../../api/generated/model'
import { getGetStockSummaryQueryKey, useRecordStockMovement } from '../../api/generated/stock/stock'
import { DecimalInput } from '../../components/DecimalInput'
import { SaveButton } from '../../components/feedback'
import { Odometer } from '../../components/Odometer'
import { StepButton } from '../../components/StepButton'
import { formatDecimalInput, formatNumber, formatSignedQuantity, readDecimal } from '../../lib/format'
import { useSaved } from '../../lib/useSaved'
import { useShake } from '../../lib/useShake'

interface MovementModalProps {
  item: ItemDto
  /** Which movement to record; null keeps the modal closed. */
  kind: ManualMovementKind | null
  onClose: () => void
}

/**
 * Records a receipt, sale, return or correction for one item. The preview below the quantity
 * only helps the user; the API computes the actual change from the stock at that moment.
 */
export function MovementModal({ item, kind, onClose }: MovementModalProps) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [quantity, setQuantity] = useState('')
  const [note, setNote] = useState('')
  const [quantityError, setQuantityError] = useState<string | null>(null)
  const saved = useSaved()
  const shake = useShake()

  const record = useRecordStockMovement({
    mutation: {
      onSuccess: async () => {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getGetItemQueryKey(item.id) }),
          queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetItemHistoryQueryKey(item.id) }),
          queryClient.invalidateQueries({ queryKey: getGetStockSummaryQueryKey() }),
        ])
        await saved.confirm()
        close()
      },
      onError: shake.shake,
    },
  })

  function close() {
    setQuantity('')
    setNote('')
    setQuantityError(null)
    record.reset()
    onClose()
  }

  const serverErrors = fieldErrorMessages(t, record.error)
  const formError = formErrorMessage(t, record.error)
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
    record.mutate({ itemId: item.id, data: { kind, quantity: value, note: note.trim() || null } })
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

          <Group gap="xs" align="flex-start" wrap="nowrap">
            <DecimalInput
              flex={1}
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
            <Group gap="xs" wrap="nowrap" mt={kind ? 26 : 0}>
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

          <TextInput
            label={isCount ? t('stock.form.noteRequired') : t('stock.form.note')}
            withAsterisk={isCount}
            value={note}
            onChange={(e) => setNote(e.currentTarget.value)}
            error={serverErrors.note}
          />

          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}

          <Group justify="flex-end" gap="sm">
            <Button variant="default" onClick={close}>
              {t('common.cancel')}
            </Button>
            <SaveButton type="submit" loading={record.isPending} saved={saved.saved}>
              {t('stock.form.submit')}
            </SaveButton>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
