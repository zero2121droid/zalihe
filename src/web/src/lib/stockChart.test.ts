import { describe, expect, it } from 'vitest'
import { belowMinimum, stockAt, steps } from './stockChart'

const history = {
  from: '2026-10-01T00:00:00Z',
  to: '2026-10-31T00:00:00Z',
  startStock: 12,
  points: [
    { at: '2026-10-05T00:00:00Z', stock: 8 },
    { at: '2026-10-10T00:00:00Z', stock: 4 },
    { at: '2026-10-20T00:00:00Z', stock: 28 },
  ],
}

describe('stockChart', () => {
  it('Steps_History_StartsWithStartStock', () => {
    // Act
    const levels = steps(history)

    // Assert
    expect(levels.map((l) => l.stock)).toEqual([12, 8, 4, 28])
  })

  it('StockAt_BetweenMovements_ReturnsLevelSinceLastMovement', () => {
    // Act & Assert
    expect(stockAt(history, Date.parse('2026-10-02T00:00:00Z'))).toBe(12)
    expect(stockAt(history, Date.parse('2026-10-12T00:00:00Z'))).toBe(4)
    expect(stockAt(history, Date.parse('2026-10-30T00:00:00Z'))).toBe(28)
  })

  it('BelowMinimum_TwoLowLevelsInARow_JoinsThemIntoOneStretch', () => {
    // Act
    const spans = belowMinimum(history, 10)

    // Assert
    expect(spans).toEqual([{ from: Date.parse('2026-10-05T00:00:00Z'), to: Date.parse('2026-10-20T00:00:00Z') }])
  })

  it('BelowMinimum_NoMinimum_ReturnsNothing', () => {
    // Act & Assert
    expect(belowMinimum(history, 0)).toEqual([])
  })
})
