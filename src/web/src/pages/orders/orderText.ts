import type { TFunction } from 'i18next'
import type { OrderLineDto } from '../../api/generated/model'

/** WooCommerce's own order statuses, in the order a shop shows them; plugins may add others. */
export const WOO_STATUSES = ['pending', 'processing', 'on-hold', 'completed', 'cancelled', 'refunded', 'failed', 'checkout-draft']

/** The translated status; a status a plugin added is shown as the shop's code. */
export function statusLabel(t: TFunction, status: string | null): string {
  if (status === null) return ''
  return WOO_STATUSES.includes(status) ? t(`orders.status.${status}`) : status
}

/** The product's name in the shop, or its ID for orders stored before names were kept. */
export function lineName(t: TFunction, line: OrderLineDto): string {
  return line.name ?? t('orders.productWithId', { id: line.externalProductId })
}

/** "https://zrno.rs" → "zrno.rs", to tell shops apart without the noise. */
export function shopHost(url: string): string {
  try {
    return new URL(url).host
  } catch {
    return url
  }
}
