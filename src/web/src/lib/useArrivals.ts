import { useEffect, useRef, useState } from 'react'

/**
 * Ids that appeared in a list since it was last shown, e.g. a movement that just arrived at the
 * top of a history. The first load and a new `scope` (another filter or page) mark nothing,
 * so only real arrivals get highlighted. Pass undefined while the list isn't final yet.
 */
export function useArrivals(ids: string[] | undefined, scope: string): ReadonlySet<string> {
  const seen = useRef<{ scope: string; ids: Set<string> } | null>(null)
  const [arrived, setArrived] = useState<ReadonlySet<string>>(() => new Set())
  const key = ids?.join(',')

  useEffect(() => {
    if (!ids) return
    const known = seen.current?.scope === scope ? seen.current.ids : null
    setArrived(new Set(known ? ids.filter((id) => !known.has(id)) : []))
    seen.current = { scope, ids: new Set([...(known ?? []), ...ids]) }
  }, [key, scope]) // oxlint-disable-line react-hooks/exhaustive-deps

  return arrived
}
