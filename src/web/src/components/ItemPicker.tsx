import { Group, Loader, Select, Stack, Text } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useListItems } from '../api/generated/items/items'
import type { ItemDto } from '../api/generated/model'
import { formatNumber } from '../lib/format'

/** Items offered while searching; typing more narrows them. */
const RESULTS = 8

interface ItemPickerProps {
  label: string
  onPick: (item: ItemDto) => void
  error?: string | null
  /** Off while the form around it is closed, so it doesn't search in the background. */
  enabled?: boolean
  autoFocus?: boolean
}

/**
 * A field that finds an item by name, SKU or barcode (the API searches, as on Artikli) and hands the
 * chosen one to the form, then empties itself for the next one. Each result shows the stock.
 */
export function ItemPicker({ label, onPick, error, enabled = true, autoFocus }: ItemPickerProps) {
  const { t, i18n } = useTranslation()
  const [text, setText] = useState('')
  const [search] = useDebouncedValue(text.trim(), 150)
  const items = useListItems(
    { search, pageSize: RESULTS },
    { query: { enabled: enabled && search !== '', placeholderData: keepPreviousData } },
  )
  const results = search ? (items.data?.items ?? []) : []

  function pick(itemId: string | null) {
    const item = results.find((i) => i.id === itemId)
    setText('')
    if (item) onPick(item)
  }

  return (
    <Select
      label={label}
      placeholder={t('common.itemPicker.placeholder')}
      leftSection={<IconSearch size={16} stroke={1.8} />}
      rightSection={items.isFetching ? <Loader size={16} /> : null}
      searchable
      searchValue={text}
      onSearchChange={setText}
      value={null}
      onChange={pick}
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
      nothingFoundMessage={search && items.isSuccess ? t('common.itemPicker.noMatches', { search }) : undefined}
      error={error}
      data-autofocus={autoFocus || undefined}
    />
  )
}
