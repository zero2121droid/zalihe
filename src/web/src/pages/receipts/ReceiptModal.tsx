import { Anchor, Group, Modal, Stack, Table, Text } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { ReceiptDto, ReceiptLineDto } from '../../api/generated/model'
import { usePermissions } from '../../auth/permissions'
import { formatDate, formatDateTime, formatMoney, formatNumber } from '../../lib/format'
import { usePhone } from '../../lib/usePhone'
import { documentLabel } from './receiptText'

const noteStyle = { borderRadius: 'var(--mantine-radius-sm)' }

/** One posted receipt: its lines with quantity, price and value. It never changes, so there is nothing to edit. */
export function ReceiptModal({ receipt, onClose }: { receipt: ReceiptDto; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const prices = usePermissions().seePurchasePrices
  const phone = usePhone()
  const currency = t('common.currency')

  return (
    <Modal opened onClose={onClose} title={t('receipts.detail.title', { document: documentLabel(t, receipt) })}>
      <Stack gap="md">
        <Group gap="lg">
          <Text fz="sm" fw={500}>
            {receipt.supplierName}
          </Text>
          <Text fz="sm" c="dimmed">
            {formatDate(receipt.documentDate, i18n.language)}
          </Text>
          <Text fz="sm" c="dimmed">
            {t('receipts.detail.posted', { date: formatDateTime(receipt.postedAt, i18n.language) })}
          </Text>
          {receipt.createdByName && (
            <Text fz="sm" c="dimmed">
              {t('receipts.detail.enteredBy', { name: receipt.createdByName })}
            </Text>
          )}
        </Group>

        {phone ? (
          <Stack gap={0}>
            {receipt.lines.map((line) => (
              <Stack key={line.id} gap={4} py="sm" style={{ borderBottom: '1px solid var(--z-line-soft)' }}>
                <ItemName line={line} />
                <Group justify="space-between">
                  <Quantity line={line} />
                  {prices && line.unitPrice !== null && (
                    <Text ff="monospace" fz="sm" c="var(--z-text-2)">
                      {formatMoney(line.unitPrice, i18n.language)} {currency}
                    </Text>
                  )}
                </Group>
              </Stack>
            ))}
          </Stack>
        ) : (
          <Table.ScrollContainer minWidth={520}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t('receipts.detail.item')}</Table.Th>
                  <Table.Th ta="right">{t('receipts.detail.quantity')}</Table.Th>
                  {prices && <Table.Th ta="right">{t('receipts.detail.unitPrice')}</Table.Th>}
                  {prices && <Table.Th ta="right">{t('receipts.detail.value')}</Table.Th>}
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {receipt.lines.map((line) => (
                  <Table.Tr key={line.id}>
                    <Table.Td>
                      <ItemName line={line} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Quantity line={line} />
                    </Table.Td>
                    {prices && (
                      <Table.Td ta="right">
                        <Money value={line.unitPrice} />
                      </Table.Td>
                    )}
                    {prices && (
                      <Table.Td ta="right">
                        <Money value={line.unitPrice === null ? null : line.unitPrice * line.quantity} />
                      </Table.Td>
                    )}
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}

        {prices && receipt.totalValue !== null && (
          <Group justify="flex-end" gap="sm">
            <Text fz="sm" c="dimmed">
              {t('receipts.detail.total')}
            </Text>
            <Text ff="monospace" fw={500}>
              {formatMoney(receipt.totalValue, i18n.language)}{' '}
              <Text component="span" fz="xs" c="dimmed">
                {currency}
              </Text>
            </Text>
          </Group>
        )}

        <Text component="div" fz="sm" c="var(--z-text-2)" p="sm" bg="var(--z-surface-2)" style={noteStyle}>
          {t('receipts.detail.fixHint')}
        </Text>
      </Stack>
    </Modal>
  )
}

function ItemName({ line }: { line: ReceiptLineDto }) {
  return (
    <div>
      <Anchor component={Link} to={`/items/${line.item.id}`} fz="sm" fw={500} c="var(--z-text)">
        {line.item.name}
      </Anchor>
      <Text ff="monospace" fz="xs" c="var(--z-text-2)">
        {line.item.sku}
      </Text>
    </div>
  )
}

function Quantity({ line }: { line: ReceiptLineDto }) {
  const { t, i18n } = useTranslation()
  return (
    <Text ff="monospace" fz="sm" style={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>
      +{formatNumber(line.quantity, i18n.language)}{' '}
      <Text component="span" fz="xs" c="dimmed">
        {t(`items.units.${line.item.unit}`)}
      </Text>
    </Text>
  )
}

function Money({ value }: { value: number | null }) {
  const { t, i18n } = useTranslation()
  if (value === null) {
    return (
      <Text fz="sm" c="dimmed">
        {t('receipts.detail.noPrice')}
      </Text>
    )
  }
  return (
    <Text ff="monospace" fz="sm" style={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>
      {formatMoney(value, i18n.language)}
    </Text>
  )
}
