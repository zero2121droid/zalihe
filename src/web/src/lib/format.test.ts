import { describe, expect, it } from 'vitest'
import { formatLongDate, formatMoney, formatNumber, formatDecimalInput, formatSignedQuantity, parseDecimal, readDecimal } from './format'

describe('formatNumber', () => {
  it('formatNumber_SerbianLatin_UsesDotForThousandsAndCommaForDecimals', () => {
    expect(formatNumber(1284350, 'sr-Latn')).toBe('1.284.350')
    expect(formatNumber(12.5, 'sr-Latn')).toBe('12,5')
  })

  it('formatNumber_English_UsesCommaForThousandsAndDotForDecimals', () => {
    expect(formatNumber(1284350, 'en')).toBe('1,284,350')
    expect(formatNumber(12.5, 'en')).toBe('12.5')
  })

  it('formatNumber_Negative_UsesTrueMinusSign', () => {
    expect(formatNumber(-2.5, 'sr-Latn')).toBe('\u22122,5')
  })

  it('formatNumber_MoreThanThreeDecimals_RoundsToQuantityPrecision', () => {
    expect(formatNumber(1.23456, 'sr-Latn')).toBe('1,235')
  })
})

describe('formatSignedQuantity', () => {
  it('formatSignedQuantity_Negative_UsesTrueMinusSign', () => {
    expect(formatSignedQuantity(-2, 'sr-Latn')).toBe('−2')
  })

  it('formatSignedQuantity_Positive_ShowsPlus', () => {
    expect(formatSignedQuantity(24, 'sr-Latn')).toBe('+24')
    expect(formatSignedQuantity(1.5, 'sr-Latn')).toBe('+1,5')
  })

  it('formatSignedQuantity_Zero_HasNoSign', () => {
    expect(formatSignedQuantity(0, 'sr-Latn')).toBe('0')
  })
})

describe('formatMoney', () => {
  it('formatMoney_SerbianLatin_FormatsWithoutTrailingZeros', () => {
    expect(formatMoney(1284350, 'sr-Latn')).toBe('1.284.350')
    expect(formatMoney(99.9, 'sr-Latn')).toBe('99,9')
  })
})

describe('formatLongDate', () => {
  it('formatLongDate_SerbianLatin_WritesWeekdayDayAndMonth', () => {
    expect(formatLongDate(new Date(2026, 9, 1), 'sr-Latn')).toBe('četvrtak, 1. oktobar')
  })
})

describe('parseDecimal', () => {
  it.each([
    ['12,5', 12.5],
    ['12.5', 12.5],
    [' 7 ', 7],
    ['1.284,5', 1284.5],
    ['1,284.5', 1284.5],
    ['-3', -3],
    ['−3', -3],
    ['0,125', 0.125],
  ])('parseDecimal_ValidInput_%s_Returns%s', (input, expected) => {
    expect(parseDecimal(input)).toBe(expected)
  })

  it.each(['', '   ', 'abc', '12,5kg', '1,2,3x'])('parseDecimal_InvalidInput_"%s"_ReturnsNull', (input) => {
    expect(parseDecimal(input)).toBeNull()
  })
})

describe('readDecimal', () => {
  it('readDecimal_EmptyInput_ReturnsNull', () => {
    expect(readDecimal('  ')).toBeNull()
  })

  it('readDecimal_CommaDecimal_ReturnsNumber', () => {
    expect(readDecimal('12,5')).toBe(12.5)
  })

  it('readDecimal_NotANumber_ReturnsInvalid', () => {
    expect(readDecimal('dvanaest')).toBe('invalid')
  })
})

describe('formatDecimalInput', () => {
  it('formatDecimalInput_SerbianLatin_UsesCommaWithoutGrouping', () => {
    expect(formatDecimalInput(1284.5, 'sr-Latn')).toBe('1284,5')
  })

  it('formatDecimalInput_Null_ReturnsEmpty', () => {
    expect(formatDecimalInput(null, 'sr-Latn')).toBe('')
  })

  it('formatDecimalInput_ReadBack_GivesSameNumber', () => {
    expect(readDecimal(formatDecimalInput(12.125, 'sr-Latn'))).toBe(12.125)
  })
})
