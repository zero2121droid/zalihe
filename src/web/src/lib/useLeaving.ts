import { useReducedMotion } from '@mantine/hooks'
import { useEffect, useState } from 'react'

/** How long a row that left the list stays to fold away (search.module.css folds in 0.3 s). */
export const LEAVING_MS = 350

export interface Shown<T> {
  item: T
  leaving: boolean
}

interface Seen<T> {
  scope: string
  key: string
  items: T[]
}

/**
 * The list to show, with rows that just dropped out of it (e.g. an order whose products were all
 * linked, under the "unlinked" filter) kept at their old place for a moment, marked `leaving`, so they
 * can fold away instead of vanishing. Only within one `scope` (filter, search, page): moving to another
 * list replaces it at once. `final` is false while the list shown is a stand-in (the previous page's
 * while the next loads): it is shown as is and compared with nothing.
 */
export function useLeaving<T>(items: T[], keyOf: (item: T) => string, scope: string, final = true): Shown<T>[] {
  const reduceMotion = useReducedMotion()
  const [seen, setSeen] = useState<Seen<T> | null>(null)
  const [leaving, setLeaving] = useState<Array<{ item: T; index: number }>>([])
  const key = items.map(keyOf).join(',')

  // Worked out while rendering, not in an effect, so a leaving row is never left out for a frame
  // (it would come back already folded, without the motion).
  if (final && (seen?.key !== key || seen.scope !== scope)) {
    const previous = seen?.scope === scope ? seen.items : []
    const kept = new Set(items.map(keyOf))
    setSeen({ scope, key, items })
    setLeaving(reduceMotion ? [] : previous.flatMap((item, index) => (kept.has(keyOf(item)) ? [] : [{ item, index }])))
  }

  useEffect(() => {
    if (leaving.length === 0) return
    const timer = setTimeout(() => setLeaving([]), LEAVING_MS)
    return () => clearTimeout(timer)
  }, [leaving])

  const shown: Shown<T>[] = items.map((item) => ({ item, leaving: false }))
  if (!final) return shown
  for (const { item, index } of leaving) {
    shown.splice(Math.min(index, shown.length), 0, { item, leaving: true })
  }
  return shown
}
