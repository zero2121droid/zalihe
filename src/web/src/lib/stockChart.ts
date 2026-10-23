import type { ItemStockHistoryDto } from '../api/generated/model'

/** A stretch of time in milliseconds since the epoch. */
export interface Span {
  from: number
  to: number
}

/** The stock as a step line: where each level starts, from the start of the period to its end. */
export function steps(history: ItemStockHistoryDto): Array<{ at: number; stock: number }> {
  return [
    { at: Date.parse(history.from), stock: history.startStock },
    ...history.points.map((p) => ({ at: Date.parse(p.at), stock: p.stock })),
  ]
}

/** The stock at a moment of the period: the last level that started at or before it. */
export function stockAt(history: ItemStockHistoryDto, at: number): number {
  let stock = history.startStock
  for (const point of history.points) {
    if (Date.parse(point.at) > at) break
    stock = point.stock
  }
  return stock
}

/** The stretches when the stock was at or below the minimum (none without a minimum). */
export function belowMinimum(history: ItemStockHistoryDto, minStock: number): Span[] {
  if (minStock <= 0) return []
  const end = Date.parse(history.to)
  const spans: Span[] = []
  const levels = steps(history)
  levels.forEach((level, i) => {
    if (level.stock > minStock) return
    const to = i + 1 < levels.length ? levels[i + 1].at : end
    const last = spans.at(-1)
    if (last && last.to === level.at) last.to = to
    else spans.push({ from: level.at, to })
  })
  return spans.filter((s) => s.to > s.from)
}
