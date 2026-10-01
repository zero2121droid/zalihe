/**
 * Number and date formatting for the UI language. Always goes through Intl (DESIGN.md:
 * "1.284.350" and "12,5" in sr-Latn); never format numbers by hand in components.
 */

const MINUS_SIGN = '−'

export function formatNumber(value: number, language: string, maximumFractionDigits = 3): string {
  return withTrueMinus(new Intl.NumberFormat(language, { maximumFractionDigits }), value)
}

/** Intl uses a hyphen for negatives; DESIGN.md asks for the real minus sign everywhere. */
function withTrueMinus(format: Intl.NumberFormat, value: number): string {
  return format
    .formatToParts(value)
    .map((part) => (part.type === 'minusSign' ? MINUS_SIGN : part.value))
    .join('')
}

/** Stock movement quantity with an explicit sign: "+24", "−2" (true minus sign, not a hyphen). */
export function formatSignedQuantity(value: number, language: string): string {
  return withTrueMinus(new Intl.NumberFormat(language, { maximumFractionDigits: 3, signDisplay: 'exceptZero' }), value)
}

export function formatMoney(value: number, language: string): string {
  return withTrueMinus(new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 2 }), value)
}

/** "četvrtak, 1. oktobar" / "Thursday, October 1". */
export function formatLongDate(date: Date, language: string): string {
  return new Intl.DateTimeFormat(language, { weekday: 'long', day: 'numeric', month: 'long' }).format(date)
}

/** "1. 10. 2026. 14:32" / "Oct 1, 2026, 2:32 PM", in the user's time zone. */
export function formatDateTime(value: string | Date, language: string): string {
  return new Intl.DateTimeFormat(language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

/**
 * Compact time for lists of recent events: "14:32" today, `yesterday` (already translated)
 * for yesterday, otherwise the date ("28. 9.").
 */
export function formatRecentTime(value: string | Date, language: string, yesterday: string, now = new Date()): string {
  const date = new Date(value)
  const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
  const days = Math.round((startOfDay(now) - startOfDay(date)) / 86_400_000)
  if (days === 0) return new Intl.DateTimeFormat(language, { hour: '2-digit', minute: '2-digit' }).format(date)
  if (days === 1) return yesterday
  return new Intl.DateTimeFormat(language, { day: 'numeric', month: 'numeric' }).format(date)
}

/**
 * A stored number shown in an input field for editing: decimal separator of the language,
 * no thousands separators (so "1284,5", not "1.284,5"), and empty for null.
 */
export function formatDecimalInput(value: number | null | undefined, language: string, maximumFractionDigits = 3): string {
  if (value === null || value === undefined) return ''
  return new Intl.NumberFormat(language, { maximumFractionDigits, useGrouping: false }).format(value)
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

/** Reads a decimal field on submit: empty is `null`, text that isn't a number is `'invalid'`. */
export function readDecimal(input: string): number | null | 'invalid' {
  if (input.trim() === '') return null
  return parseDecimal(input) ?? 'invalid'
}
