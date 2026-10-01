/**
 * Number and date formatting for the UI language. Always goes through Intl (DESIGN.md:
 * "1.284.350" and "12,5" in sr-Latn); never format numbers by hand in components.
 */

const MINUS_SIGN = '−'

export function formatNumber(value: number, language: string, maximumFractionDigits = 3): string {
  return new Intl.NumberFormat(language, { maximumFractionDigits }).format(value)
}

/** Stock movement quantity with an explicit sign: "+24", "−2" (true minus sign, not a hyphen). */
export function formatSignedQuantity(value: number, language: string): string {
  return new Intl.NumberFormat(language, { maximumFractionDigits: 3, signDisplay: 'exceptZero' })
    .formatToParts(value)
    .map((part) => (part.type === 'minusSign' ? MINUS_SIGN : part.value))
    .join('')
}

export function formatMoney(value: number, language: string): string {
  return new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(value)
}

/** "četvrtak, 1. oktobar" / "Thursday, October 1". */
export function formatLongDate(date: Date, language: string): string {
  return new Intl.DateTimeFormat(language, { weekday: 'long', day: 'numeric', month: 'long' }).format(date)
}

/**
 * Parses a decimal typed by the user. Accepts both "12,5" and "12.5"; when both separators
 * appear, the last one is the decimal separator ("1.284,5" or "1,284.5").
 * Returns null for anything that isn't a number.
 */
export function parseDecimal(input: string): number | null {
  const value = input.trim().replace(/\s/g, '').replace(MINUS_SIGN, '-')
  if (value === '') return null

  const decimalIndex = Math.max(value.lastIndexOf(','), value.lastIndexOf('.'))
  const normalized =
    decimalIndex === -1
      ? value
      : value.slice(0, decimalIndex).replace(/[.,]/g, '') + '.' + value.slice(decimalIndex + 1)

  return /^-?\d+(\.\d+)?$/.test(normalized) ? Number(normalized) : null
}
