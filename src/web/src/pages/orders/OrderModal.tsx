import { Anchor, Button, Group, Modal, Stack, Table, Text } from '@mantine/core'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { OrderDto, OrderLineDto } from '../../api/generated/model'
import { formatDateTime, formatNumber } from '../../lib/format'
import { usePhone } from '../../lib/usePhone'
import { lineName, shopHost, statusLabel } from './orderText'

const noteStyle = { borderRadius: 'var(--mantine-radius-sm)' }

/** One order's products, the items they are linked to and what each took from stock. */
export function OrderModal({ order, onClose }: { order: OrderDto; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const hasUnlinked = order.lines.some((l) => l.state === 'unlinked')
  const hasLinkedLater = order.lines.some((l) => l.state === 'linkedAfterOrder')
  const phone = usePhone()

  return (
    <Modal opened onClose={onClose} title={t('orders.detail.title', { number: order.number })}>
      <Stack gap="md">
        <Group gap="lg">
          <Text fz="sm">
            {statusLabel(t, order.status)}
            <Text component="span" fz="sm" c="dimmed">
              {' · '}
              {order.takesStock ? t('orders.takesStock') : t('orders.doesNotTakeStock')}
            </Text>
          </Text>
          <Text fz="sm" c="dimmed">
            {t('orders.detail.placedAt', { date: formatDateTime(order.placedAt, i18n.language) })}
          </Text>
          <Text fz="sm" c="dimmed">
            {shopHost(order.shopUrl)}
          </Text>
        </Group>

        {phone ? (
          // On a phone each product is a block (shop product, item, both quantities), not a wide table row.
          <Stack gap={0}>
            {order.lines.map((line) => (
              <PhoneLine key={line.externalProductId} line={line} />
            ))}
          </Stack>
        ) : (
          <Table.ScrollContainer minWidth={560}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t('orders.detail.shopProduct')}</Table.Th>
                  <Table.Th>{t('orders.detail.item')}</Table.Th>
                  <Table.Th ta="right">{t('orders.detail.ordered')}</Table.Th>
                  <Table.Th ta="right">{t('orders.detail.applied')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {order.lines.map((line) => (
                  <LineRow key={line.externalProductId} line={line} />
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}

        {!order.takesStock && <Note>{t('orders.detail.inactive')}</Note>}
        {hasLinkedLater && <Note>{t('orders.detail.linkedAfterOrder')}</Note>}
        {hasUnlinked && (
          <Note>
            <Stack gap="sm" align="flex-start">
              <Text fz="sm">{t('orders.detail.unlinked')}</Text>
              <Button variant="default" component={Link} to={`/channels/${order.channelId}/import`}>
                {t('orders.detail.linkUnlinked')}
              </Button>
            </Stack>
          </Note>
        )}
      </Stack>
    </Modal>
  )
}

function LineRow({ line }: { line: OrderLineDto }) {
  const { t, i18n } = useTranslation()
  const unit = line.item ? t(`items.units.${line.item.unit}`) : null

  return (
    <Table.Tr>
      <Table.Td>
        <Text fz="sm">{lineName(t, line)}</Text>
      </Table.Td>
      <Table.Td>
        <LinkedItem line={line} />
      </Table.Td>
      <Table.Td ta="right">
        <Quantity value={line.orderedQuantity} unit={unit} language={i18n.language} />
      </Table.Td>
      <Table.Td ta="right">
        <Quantity value={line.appliedQuantity} unit={unit} language={i18n.language} />
      </Table.Td>
    </Table.Tr>
  )
}

function PhoneLine({ line }: { line: OrderLineDto }) {
  const { t, i18n } = useTranslation()
  const unit = line.item ? t(`items.units.${line.item.unit}`) : null

  return (
    <Stack gap={6} py="sm" style={{ borderBottom: '1px solid var(--z-line-soft)' }}>
      <Text fz="xs" c="dimmed">
        {lineName(t, line)}
      </Text>
      <div>
        <LinkedItem line={line} />
      </div>
      <Group gap="lg">
        <Group gap={6}>
          <Text fz="xs" c="dimmed">
            {t('orders.detail.ordered')}
          </Text>
          <Quantity value={line.orderedQuantity} unit={unit} language={i18n.language} />
        </Group>
        <Group gap={6}>
          <Text fz="xs" c="dimmed">
            {t('orders.detail.applied')}
          </Text>
          <Quantity value={line.appliedQuantity} unit={unit} language={i18n.language} />
        </Group>
      </Group>
    </Stack>
  )
}

/** The item a shop product is linked to, or that it isn't linked yet. */
function LinkedItem({ line }: { line: OrderLineDto }) {
  const { t } = useTranslation()
  return (
    <>
      {line.item ? (
        <>
          <Anchor component={Link} to={`/items/${line.item.id}`} fz="sm" fw={500} c="var(--z-text)">
            {line.item.name}
          </Anchor>
          <Text ff="monospace" fz="xs" c="var(--z-text-2)">
            {line.item.sku}
          </Text>
          {line.state === 'linkedAfterOrder' && (
            <Text fz="xs" c="dimmed">
              {t('orders.lineState.linkedAfterOrder')}
            </Text>
          )}
      </>
    ) : (
      <Text fz="sm" c="var(--z-status-low)">
        {t('orders.lineState.unlinked')}
      </Text>
    )}
    </>
  )
}

function Quantity({ value, unit, language }: { value: number; unit: string | null; language: string }) {
  return (
    <Text ff="monospace" fz="sm" style={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>
      {formatNumber(value, language)}
      {unit && (
        <Text component="span" fz="xs" c="dimmed">
          {' '}
          {unit}
        </Text>
      )}
    </Text>
  )
}

function Note({ children }: { children: ReactNode }) {
  return (
    <Text component="div" fz="sm" c="var(--z-text-2)" p="sm" bg="var(--z-surface-2)" style={noteStyle}>
      {children}
    </Text>
  )
}
