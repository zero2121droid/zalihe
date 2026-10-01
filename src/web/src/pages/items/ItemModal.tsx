import { Autocomplete, Button, Group, Modal, Select, SimpleGrid, Stack, Text, TextInput } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import {
  getGetItemQueryKey,
  getListItemCategoriesQueryKey,
  getListItemsQueryKey,
  useActivateItem,
  useCreateItem,
  useDeactivateItem,
  useListItemCategories,
  useUpdateItem,
} from '../../api/generated/items/items'
import { type ItemDto, Unit } from '../../api/generated/model'
import { DecimalInput } from '../../components/DecimalInput'
import { formatDecimalInput, readDecimal } from '../../lib/format'

interface Fields {
  name: string
  sku: string
  barcode: string
  unit: Unit
  minStock: string
  category: string
  groupName: string
  purchasePrice: string
  salePrice: string
}

const emptyFields: Fields = {
  name: '',
  sku: '',
  barcode: '',
  unit: Unit.kom,
  minStock: '',
  category: '',
  groupName: '',
  purchasePrice: '',
  salePrice: '',
}

function fieldsFrom(item: ItemDto, language: string): Fields {
  return {
    name: item.name,
    sku: item.sku,
    barcode: item.barcode ?? '',
    unit: item.unit,
    minStock: formatDecimalInput(item.minStock, language),
    category: item.category ?? '',
    groupName: item.groupName ?? '',
    purchasePrice: formatDecimalInput(item.purchasePrice, language, 2),
    salePrice: formatDecimalInput(item.salePrice, language, 2),
  }
}

const decimalFields = ['minStock', 'purchasePrice', 'salePrice'] as const

interface ItemModalProps {
  opened: boolean
  onClose: () => void
  /** The item to edit; without it the modal creates a new item. */
  item?: ItemDto | null
}

/**
 * Create and edit form in one. The parent gives it a `key` per item, so the fields start
 * from that item's data each time it opens.
 */
export function ItemModal({ opened, onClose, item }: ItemModalProps) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [fields, setFields] = useState<Fields>(() => (item ? fieldsFrom(item, i18n.language) : emptyFields))
  const [localErrors, setLocalErrors] = useState<Partial<Record<keyof Fields, string>>>({})
  const categories = useListItemCategories({ query: { enabled: opened } })

  async function refreshAndClose() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
      queryClient.invalidateQueries({ queryKey: getListItemCategoriesQueryKey() }),
      item && queryClient.invalidateQueries({ queryKey: getGetItemQueryKey(item.id) }),
    ])
    close()
  }

  const createItem = useCreateItem({ mutation: { onSuccess: refreshAndClose } })
  const updateItem = useUpdateItem({ mutation: { onSuccess: refreshAndClose } })
  const deactivateItem = useDeactivateItem({ mutation: { onSuccess: refreshAndClose } })
  const activateItem = useActivateItem({ mutation: { onSuccess: refreshAndClose } })
  const save = item ? updateItem : createItem
  const toggleActive = item?.isActive ? deactivateItem : activateItem

  const serverErrors = fieldErrorMessages(t, save.error)
  const formError = formErrorMessage(t, save.error ?? toggleActive.error)
  const errorFor = (field: keyof Fields) => localErrors[field] ?? serverErrors[field]

  function close() {
    setFields(item ? fieldsFrom(item, i18n.language) : emptyFields)
    setLocalErrors({})
    for (const mutation of [createItem, updateItem, deactivateItem, activateItem]) mutation.reset()
    onClose()
  }

  function set<K extends keyof Fields>(field: K, value: Fields[K]) {
    setFields((current) => ({ ...current, [field]: value }))
    setLocalErrors((current) => ({ ...current, [field]: undefined }))
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()

    // Only the number format is checked here; every business rule is validated by the API.
    const numbers = Object.fromEntries(decimalFields.map((f) => [f, readDecimal(fields[f])]))
    const invalid = decimalFields.filter((f) => numbers[f] === 'invalid')
    if (invalid.length > 0) {
      setLocalErrors(Object.fromEntries(invalid.map((f) => [f, t('errors.validation.number')])))
      return
    }

    const data = {
      name: fields.name,
      sku: fields.sku,
      barcode: fields.barcode || null,
      unit: fields.unit,
      category: fields.category || null,
      groupName: fields.groupName || null,
      minStock: (numbers.minStock as number | null) ?? 0,
      purchasePrice: numbers.purchasePrice as number | null,
      salePrice: numbers.salePrice as number | null,
    }
    if (item) updateItem.mutate({ id: item.id, data })
    else createItem.mutate({ data })
  }

  const currency = (
    <Text fz="xs" c="dimmed" pr="xs">
      {t('common.currency')}
    </Text>
  )

  return (
    <Modal
      opened={opened}
      onClose={close}
      title={item ? t('items.form.editTitle') : t('items.form.title')}
    >
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {item && !item.isActive && (
            <Text fz="sm" c="dimmed" p="sm" bg="var(--z-surface-2)" style={{ borderRadius: 'var(--mantine-radius-sm)' }}>
              {t('items.form.inactiveNote')}
            </Text>
          )}

          <TextInput
            label={t('items.form.name')}
            withAsterisk
            data-autofocus
            value={fields.name}
            onChange={(e) => set('name', e.currentTarget.value)}
            error={errorFor('name')}
          />

          <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="md">
            <TextInput
              label={t('items.form.sku')}
              withAsterisk
              styles={{ input: { fontFamily: 'var(--mantine-font-family-monospace)' } }}
              value={fields.sku}
              onChange={(e) => set('sku', e.currentTarget.value)}
              error={errorFor('sku')}
            />
            <TextInput
              label={t('items.form.barcode')}
              inputMode="numeric"
              value={fields.barcode}
              onChange={(e) => set('barcode', e.currentTarget.value)}
              error={errorFor('barcode')}
            />

            <Select
              label={t('items.form.unit')}
              withAsterisk
              allowDeselect={false}
              data={Object.values(Unit).map((unit) => ({ value: unit, label: t(`items.units.${unit}`) }))}
              value={fields.unit}
              onChange={(value) => value && set('unit', value as Unit)}
              error={errorFor('unit')}
            />
            <DecimalInput
              label={t('items.form.minStock')}
              description={t('items.form.minStockHint')}
              inputWrapperOrder={['label', 'input', 'description', 'error']}
              placeholder="0"
              rightSection={
                <Text fz="xs" c="dimmed" pr="xs">
                  {t(`items.units.${fields.unit}`)}
                </Text>
              }
              value={fields.minStock}
              onChange={(value) => set('minStock', value)}
              error={errorFor('minStock')}
            />

            <Autocomplete
              label={t('items.form.category')}
              data={categories.data ?? []}
              value={fields.category}
              onChange={(value) => set('category', value)}
              error={errorFor('category')}
            />
            <TextInput
              label={t('items.form.groupName')}
              description={t('items.form.groupNameHint')}
              inputWrapperOrder={['label', 'input', 'description', 'error']}
              value={fields.groupName}
              onChange={(e) => set('groupName', e.currentTarget.value)}
              error={errorFor('groupName')}
            />

            <DecimalInput
              label={t('items.form.purchasePrice')}
              rightSection={currency}
              rightSectionWidth={48}
              value={fields.purchasePrice}
              onChange={(value) => set('purchasePrice', value)}
              error={errorFor('purchasePrice')}
            />
            <DecimalInput
              label={t('items.form.salePrice')}
              rightSection={currency}
              rightSectionWidth={48}
              value={fields.salePrice}
              onChange={(value) => set('salePrice', value)}
              error={errorFor('salePrice')}
            />
          </SimpleGrid>

          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}

          <Group justify="space-between" gap="sm">
            {item ? (
              <Button
                variant="default"
                loading={toggleActive.isPending}
                onClick={() => toggleActive.mutate({ id: item.id })}
              >
                {item.isActive ? t('items.form.deactivate') : t('items.form.activate')}
              </Button>
            ) : (
              <span />
            )}
            <Group gap="sm">
              <Button variant="default" onClick={close}>
                {t('common.cancel')}
              </Button>
              <Button type="submit" loading={save.isPending}>
                {t('items.form.submit')}
              </Button>
            </Group>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
