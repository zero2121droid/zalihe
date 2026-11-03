import type { TFunction } from 'i18next'
import type { OrderDto, OrderLineDto } from '../../api/generated/model'

/** WooCommerce's own order statuses, in the order a shop shows them; plugins may add others. */
export const WOO_STATUSES = ['pending', 'processing', 'on-hold', 'completed', 'cancelled', 'refunded', 'failed', 'checkout-draft']

/** A manual order's statuses that aren't also the shop's ("cancelled" is shared). */
export const MANUAL_STATUSES = ['confirmed']

/** The statuses to filter by: the shop's, then the manual order's own. */
export const FILTER_STATUSES = [...WOO_STATUSES, ...MANUAL_STATUSES]

/** The translated status; a status a plugin added is shown as the shop's code. */
export function statusLabel(t: TFunction, status: string | null): string {
  if (status === null) return ''
  return FILTER_STATUSES.includes(status) ? t(`orders.status.${status}`) : status
}

/** A shop order's number with "#" ("#1041"); a manual one carries its own prefix ("R-0001"). */
export function orderNumber(t: TFunction, order: OrderDto): string {
  return order.source === 'manual' ? order.number : t('orders.number', { number: order.number })
}

/** Where the order came from: the shop's address, or "Ručno". */
export function orderOrigin(t: TFunction, order: OrderDto): string {
  return order.shopUrl === null ? t('orders.manual') : shopHost(order.shopUrl)
}

/**
 * The product's name in the shop; for a manual order's line, which has no shop product, the item's
 * name; for orders stored before names were kept, the product's ID.
 */
export function lineName(t: TFunction, line: OrderLineDto): string {
  return line.name ?? line.item?.name ?? t('orders.productWithId', { id: line.externalProductId })
}

/** "https://zrno.rs" → "zrno.rs", to tell shops apart without the noise. */
export function shopHost(url: string): string {
  try {
    return new URL(url).host
  } catch {
    return url
  }
}
