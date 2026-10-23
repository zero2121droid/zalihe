import { ActionIcon, CloseButton, Loader, Modal, Text } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconCamera, IconSearch } from '@tabler/icons-react'
import { keepPreviousData } from '@tanstack/react-query'
import { type KeyboardEvent, useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { useListItems } from '../api/generated/items/items'
import { canUseCamera } from '../lib/barcodeDetector'
import { BarcodeScanner } from './BarcodeScanner'
import { SearchMatch } from './search'
import classes from './QuickSearch.module.css'
import { StockQuantity, StockStatusLabel } from './stock'

/** Items shown at most; the full list is a click away on Artikli. */
const RESULTS = 8

/**
 * Jump to an item by name, SKU or barcode from anywhere (Ctrl+K or ⌘K, the button in the menu, the
 * magnifier on a phone). Each result shows the stock, so the answer is often there without opening it.
 * Arrows move through the results, Enter opens one, Escape closes. On a phone the window fills the
 * screen and there is no Escape, so an X closes it instead. Where there is a camera, a barcode can be
 * scanned into the field; when it is exactly one item's barcode or SKU, that item opens at once.
 */
export function QuickSearch({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const listId = useId()
  const [text, setText] = useState('')
  const [search] = useDebouncedValue(text.trim(), 150)
  const [active, setActive] = useState(0)
  const items = useListItems(
    { search, pageSize: RESULTS },
    { query: { enabled: search !== '', placeholderData: keepPreviousData } },
  )
  const results = search ? (items.data?.items ?? []) : []
  const current = Math.min(active, results.length - 1)
  const optionId = (index: number) => `${listId}-${index}`
  const [scanning, setScanning] = useState(false)
  // The code just scanned, until its results arrive: then the matching item opens by itself.
  const scanned = useRef<string | null>(null)

  useEffect(() => {
    const code = scanned.current
    if (!code || search !== code || !items.isSuccess || items.isPlaceholderData) return
    scanned.current = null
    const first = items.data.items[0]
    if (first && (first.barcode === code || first.sku.toLowerCase() === code.toLowerCase())) {
      onClose()
      void navigate(`/items/${first.id}`)
    }
  }, [search, items.isSuccess, items.isPlaceholderData, items.data, navigate, onClose])

  function onScanned(code: string) {
    scanned.current = code
    setScanning(false)
    setText(code)
    setActive(0)
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      const step = event.key === 'ArrowDown' ? 1 : -1
      setActive((current + step + results.length) % Math.max(results.length, 1))
    } else if (event.key === 'Enter' && results[current]) {
      event.preventDefault()
      onClose()
      void navigate(`/items/${results[current].id}`)
    }
  }

  return (
    <Modal opened onClose={onClose} withCloseButton={false} padding={0} size={640} centered={false} yOffset="12vh">
      <div className={classes.query}>
        <IconSearch size={18} stroke={1.8} aria-hidden />
        <input
          className={classes.input}
          aria-label={t('common.quickSearch.label')}
          placeholder={t('common.quickSearch.label')}
          role="combobox"
          aria-expanded={results.length > 0}
          aria-controls={listId}
          aria-activedescendant={results.length > 0 ? optionId(current) : undefined}
          autoComplete="off"
          data-autofocus
          value={text}
          onChange={(e) => {
            setText(e.currentTarget.value)
            setActive(0)
          }}
          onKeyDown={onKeyDown}
        />
        {canUseCamera() && !scanning && (
          <ActionIcon onClick={() => setScanning(true)} aria-label={t('common.quickSearch.camera.open')}>
            <IconCamera size={18} stroke={1.8} />
          </ActionIcon>
        )}
        {items.isFetching && search !== '' ? <Loader size={16} /> : <span className={classes.kbd}>Esc</span>}
        <CloseButton className={classes.close} onClick={onClose} aria-label={t('common.close')} />
      </div>

      {scanning && <BarcodeScanner onDetected={onScanned} onCancel={() => setScanning(false)} />}

      <div className={classes.list} id={listId} role="listbox" aria-label={t('common.quickSearch.results')} hidden={scanning}>
        {results.map((item, index) => (
          <Link
            key={item.id}
            id={optionId(index)}
            to={`/items/${item.id}`}
            role="option"
            aria-selected={index === current}
            tabIndex={-1}
            className={classes.option}
            onMouseEnter={() => setActive(index)}
            onClick={onClose}
          >
            <div style={{ minWidth: 0 }}>
              <Text fw={500} truncate>
                <SearchMatch text={item.name} term={search} />
              </Text>
              <Text ff="monospace" fz="xs" c="var(--z-text-2)" truncate>
                <SearchMatch text={item.sku} term={search} />
              </Text>
            </div>
            <div className={classes.side}>
              <StockQuantity quantity={item.stock} unit={item.unit} status={item.status} size={16} />
              <StockStatusLabel status={item.status} />
            </div>
          </Link>
        ))}
        {search === '' && (
          <Text className={classes.note} fz="sm" c="dimmed">
            {t('common.quickSearch.hint')}
          </Text>
        )}
        {search !== '' && items.isSuccess && !items.isPlaceholderData && results.length === 0 && (
          <Text className={classes.note} fz="sm" c="dimmed" role="status">
            {t('common.quickSearch.empty', { search })}
          </Text>
        )}
      </div>

      <Text className={classes.keys} fz="xs" c="dimmed" hidden={scanning}>
        {t('common.quickSearch.keys')}
      </Text>
    </Modal>
  )
}
