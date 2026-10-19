import { Box, Button, Group, Loader, SimpleGrid, Stack, Table, Text, TextInput, Title } from '@mantine/core'
import type { ReactNode } from 'react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { StockStatus } from '../../api/generated/model'
import { Eyebrow } from '../../components/Eyebrow'
import { SaveButton } from '../../components/feedback'
import { SearchBody, SearchCell, SearchMatch, SearchRow } from '../../components/search'
import { BoxLoader, EmptyBox, PageLoader, ShopImportAnimation, SkeletonRows } from '../../components/loaders'
import { MovementQuantity, StockQuantity, StockStatusLabel } from '../../components/stock'
import stockClasses from '../../components/stock.module.css'
import { useArrivals } from '../../lib/useArrivals'
import { matchesSearch } from '../../lib/search'
import { useSaved } from '../../lib/useSaved'
import { useShake } from '../../lib/useShake'

/**
 * Development only (registered in router.tsx under import.meta.env.DEV): every animation running
 * all the time, so they can be looked at without a slow request. Section labels are component
 * names, not UI text; everything a user would read comes from the locale files.
 */

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }
const MIN_STOCK = 10

function Specimen({ name, children }: { name: string; children: ReactNode }) {
  return (
    <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Box px="lg" py="sm" style={{ borderBottom: '1px solid var(--z-line-soft)' }}>
        <Eyebrow>{name}</Eyebrow>
      </Box>
      <Box p="lg">{children}</Box>
    </Box>
  )
}

function statusOf(quantity: number): StockStatus {
  if (quantity <= 0) return 'outOfStock'
  return quantity < MIN_STOCK ? 'low' : 'inStock'
}

function StockChangeDemo() {
  const { t } = useTranslation()
  const [quantity, setQuantity] = useState(31)
  const [movements, setMovements] = useState<Array<{ id: string; quantity: number }>>([])
  const arrivals = useArrivals(
    movements.map((m) => m.id),
    'demo',
  )

  function move(delta: number) {
    const next = Math.max(0, quantity + delta)
    if (next === quantity) return
    setQuantity(next)
    setMovements((list) => [{ id: crypto.randomUUID(), quantity: next - quantity }, ...list].slice(0, 4))
  }

  return (
    <Stack gap="md">
      <Group gap="xl" align="center">
        <StockQuantity quantity={quantity} unit="kom" status={statusOf(quantity)} size={26} animate />
        <StockStatusLabel status={statusOf(quantity)} pulse />
      </Group>
      <Group gap="xs">
        <Button variant="default" onClick={() => move(24)} rightSection={<MovementQuantity quantity={24} />}>
          {t('stock.actions.receipt')}
        </Button>
        <Button variant="default" onClick={() => move(-2)} rightSection={<MovementQuantity quantity={-2} />}>
          {t('stock.actions.sale')}
        </Button>
        <Button variant="default" onClick={() => move(-15)} rightSection={<MovementQuantity quantity={-15} />}>
          {t('stock.actions.sale')}
        </Button>
      </Group>
      {movements.length > 0 && (
        <Table>
          <Table.Tbody>
            {movements.map((m) => (
              <Table.Tr key={m.id} className={arrivals.has(m.id) ? stockClasses.freshRow : undefined}>
                <Table.Td>{t(m.quantity > 0 ? 'stock.types.receipt' : 'stock.types.sale')}</Table.Td>
                <Table.Td ta="right">
                  <MovementQuantity quantity={m.quantity} />
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}
    </Stack>
  )
}

// Sample data, not UI text.
const SEARCH_ITEMS = [
  ['Čarape, sive, 39–42', 'CAR-SIV-39'],
  ['Kapa zimska, teget', 'KAP-TEG-U'],
  ['Majica pamuk, crna, M', 'MAJ-CRN-M'],
  ['Majica pamuk, bela, L', 'MAJ-BEL-L'],
  ['Ranac platneni, zeleni', 'RAN-ZEL-U'],
  ['Kapa letnja, bež', 'KAP-BEZ-U'],
]

function SearchDemo() {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  return (
    <Stack gap="sm">
      <TextInput
        type="search"
        aria-label={t('items.search')}
        placeholder={t('items.searchPlaceholder')}
        value={search}
        onChange={(e) => setSearch(e.currentTarget.value)}
      />
      <Table>
        <SearchBody narrowing>
          {SEARCH_ITEMS.map(([name, sku]) => (
            <SearchRow key={sku} hidden={!matchesSearch(search, name, sku)}>
              <Table.Td>
                <SearchCell>
                  <SearchMatch text={name} term={search} />
                </SearchCell>
              </Table.Td>
              <Table.Td>
                <SearchCell>
                  <Text ff="monospace" fz="xs" c="var(--z-text-2)">
                    <SearchMatch text={sku} term={search} />
                  </Text>
                </SearchCell>
              </Table.Td>
            </SearchRow>
          ))}
        </SearchBody>
      </Table>
    </Stack>
  )
}

function SavedDemo() {
  const { t } = useTranslation()
  const saved = useSaved()
  const [saving, setSaving] = useState(false)

  async function save() {
    setSaving(true)
    await new Promise((resolve) => setTimeout(resolve, 900))
    setSaving(false)
    await saved.confirm()
  }

  return (
    <Group justify="center" py="md">
      <SaveButton onClick={save} loading={saving} saved={saved.saved}>
        {t('items.form.submit')}
      </SaveButton>
    </Group>
  )
}

function ShakeDemo() {
  const { t } = useTranslation()
  const shake = useShake()
  return (
    <Stack className={shake.className} gap="sm" align="flex-start">
      <TextInput label={t('stock.form.quantity.sale')} error={t('errors.validation.required')} w="100%" />
      <Button variant="default" onClick={shake.shake}>
        {t('stock.form.submit')}
      </Button>
    </Stack>
  )
}

export function AnimationsPage() {
  const { t } = useTranslation()
  return (
    <Box p="xl" maw={1180}>
      <Stack gap="lg">
        <Title order={1}>/dev/animacije</Title>
        <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
          <Specimen name="A · PageLoader">
            <PageLoader placement="inline" />
          </Specimen>
          <Specimen name="B · Loader, Button loading">
            <Group justify="center" gap="xl" py="md">
              <Loader />
              <Loader size="sm" />
              <Button loading>{t('stock.form.submit')}</Button>
            </Group>
          </Specimen>
          <Specimen name="C · BoxLoader">
            <BoxLoader label={t('items.detail.opening')} placement="inline" />
          </Specimen>
          <Specimen name="F · SkeletonRows">
            <Box bd="1px solid var(--z-line-soft)" style={sectionStyle}>
              <SkeletonRows rows={4} />
            </Box>
          </Specimen>
          <Specimen name="D · StockQuantity animate, StockStatusLabel pulse, freshRow">
            <StockChangeDemo />
          </Specimen>
          <Specimen name="E · ShopImportAnimation">
            <Stack gap="xs" align="center">
              <ShopImportAnimation />
              <Text fw={500}>{t('channels.import.importing')}</Text>
              <Text fz="sm" c="dimmed">
                {t('channels.import.importingNote')}
              </Text>
            </Stack>
          </Specimen>
          <Specimen name="S · SearchMatch, SearchRow">
            <SearchDemo />
          </Specimen>
          <Specimen name="H · SaveButton, useSaved">
            <SavedDemo />
          </Specimen>
          <Specimen name="J · useShake">
            <ShakeDemo />
          </Specimen>
          <Specimen name="G · EmptyBox">
            <Stack gap="sm" align="flex-start">
              <EmptyBox />
              <Title order={2}>{t('items.empty.title')}</Title>
              <Text c="dimmed">{t('items.empty.description')}</Text>
            </Stack>
          </Specimen>
        </SimpleGrid>
      </Stack>
    </Box>
  )
}
