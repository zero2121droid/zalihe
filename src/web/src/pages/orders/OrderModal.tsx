import { Anchor, Button, Group, Modal, Stack, Table, Text } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import { getListItemsQueryKey } from '../../api/generated/items/items'
import type { OrderDto, OrderLineDto } from '../../api/generated/model'
import { getListOrdersQueryKey, useSetOrderStatus } from '../../api/generated/orders/orders'
import { getGetStockSummaryQueryKey } from '../../api/generated/stock/stock'
import { usePermissions } from '../../auth/permissions'
import { formatDateTime, formatNumber } from '../../lib/format'
import { usePhone } from '../../lib/usePhone'
import { lineName, orderNumber, shopHost, statusLabel } from './orderText'

const noteStyle = { borderRadius: 'var(--mantine-radius-sm)' }

/**
 * One order's products, the items they are linked to and what each took from stock. A manual order
 * has no shop products, only items, and can be cancelled (its goods go back to stock) or confirmed again.
 */
export function OrderModal({ order, onClose }: { order: OrderDto; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const can = usePermissions()
  const manual = order.source === 'manual'
  const hasUnlinked = order.lines.some((l) => l.state === 'unlinked')
  const hasLinkedLater = order.lines.some((l) => l.state === 'linkedAfterOrder')
  const phone = usePhone()

  return (
    <Modal opened onClose={onClose} title={t('orders.detail.title', { number: orderNumber(t, order) })}>
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
            {order.shopUrl === null ? t('orders.detail.manual') : shopHost(order.shopUrl)}
          </Text>
          {order.reference && (
            <Text fz="sm" c="dimmed">
              {t('orders.detail.reference', { reference: order.reference })}
            </Text>
          )}
        </Group>

        {order.note && (
          <Text fz="sm" c="var(--z-text-2)" style={{ whiteSpace: 'pre-wrap' }}>
            {order.note}
          </Text>
        )}

        {phone ? (
          // On a phone each product is a block (shop product, item, both quantities), not a wide table row.
          <Stack gap={0}>
            {order.lines.map((line) => (
              <PhoneLine key={line.externalProductId} line={line} manual={manual} />
            ))}
          </Stack>
        ) : (
          <Table.ScrollContainer minWidth={560}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  {!manual && <Table.Th>{t('orders.detail.shopProduct')}</Table.Th>}
                  <Table.Th>{t('orders.detail.item')}</Table.Th>
                  <Table.Th ta="right">{t('orders.detail.ordered')}</Table.Th>
                  <Table.Th ta="right">{t('orders.detail.applied')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {order.lines.map((line) => (
                  <LineRow key={line.externalProductId} line={line} manual={manual} />
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}

        {!order.takesStock && <Note>{manual ? t('orders.detail.manualInactive') : t('orders.detail.inactive')}</Note>}
        {hasLinkedLater && <Note>{t('orders.detail.linkedAfterOrder')}</Note>}
        {hasUnlinked && (
          <Note>
            <Stack gap="sm" align="flex-start">
              <Text fz="sm">{t('orders.detail.unlinked')}</Text>
              {can.manage && (
                <Button variant="default" component={Link} to={`/channels/${order.channelId}/import`}>
                  {t('orders.detail.linkUnlinked')}
                </Button>
              )}
            </Stack>
          </Note>
        )}

        {manual && <StatusAction order={order} />}
      </Stack>
    </Modal>
  )
}

/** Cancels a confirmed manual order, or confirms a cancelled one again; the list refreshes the window. */
function StatusAction({ order }: { order: OrderDto }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const cancelled = order.status === 'cancelled'
  const setStatus = useSetOrderStatus({
    mutation: {
      onSuccess: () =>
        Promise.all([
          queryClient.invalidateQueries({ queryKey: getListOrdersQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetStockSummaryQueryKey() }),
        ]),
    },
  })
  const error = formErrorMessage(t, setStatus.error)

  return (
    <Stack gap="xs" align="flex-end">
      {error && (
        <Text role="alert" c="var(--mantine-color-error)" fz="sm">
          {error}
        </Text>
      )}
      <Button
        variant="default"
        loading={setStatus.isPending}
        onClick={() => setStatus.mutate({ id: order.id, data: { status: cancelled ? 'confirmed' : 'cancelled' } })}
      >
        {cancelled ? t('orders.detail.confirm') : t('orders.detail.cancel')}
      </Button>
    </Stack>
  )
}

function LineRow({ line, manual }: { line: OrderLineDto; manual: boolean }) {
  const { t, i18n } = useTranslation()
  const unit = line.item ? t(`items.units.${line.item.unit}`) : null

  return (
    <Table.Tr>
      {!manual && (
        <Table.Td>
          <Text fz="sm">{lineName(t, line)}</Text>
        </Table.Td>
      )}
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

function PhoneLine({ line, manual }: { line: OrderLineDto; manual: boolean }) {
  const { t, i18n } = useTranslation()
  const unit = line.item ? t(`items.units.${line.item.unit}`) : null

  return (
    <Stack gap={6} py="sm" style={{ borderBottom: '1px solid var(--z-line-soft)' }}>
      {!manual && (
        <Text fz="xs" c="dimmed">
          {lineName(t, line)}
        </Text>
      )}
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
