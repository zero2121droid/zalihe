import type { TFunction } from 'i18next'
import type { ReceiptDto } from '../../api/generated/model'

/** The document's number, or "bez broja" for a receipt entered without one. */
export function documentLabel(t: TFunction, receipt: Pick<ReceiptDto, 'documentNumber'>): string {
  return receipt.documentNumber ?? t('receipts.noDocument')
}
