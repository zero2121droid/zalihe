import { type RefObject, useLayoutEffect, useRef } from 'react'

const DURATION_MS = 450
const STAGGER_MS = 25
const MAX_STAGGER_MS = 250

/**
 * Rows slide from where they were to where they land when the list is put in a new order (M from
 * the animation sheet), instead of jumping. `order` names the order (e.g. "stock:desc"): when it
 * changes, the rows' places are noted; when the new rows arrive, each row still on the page moves
 * from its old place to the new one, and rows new to the page simply appear. Rows carry their key
 * in `data-flip`. Nothing moves under prefers-reduced-motion, or when the rows change for another
 * reason (paging, search).
 */
export function useFlipRows(container: RefObject<HTMLElement | null>, keys: string[], order: string) {
  const before = useRef<Map<string, number> | null>(null)
  const shownOrder = useRef(order)
  const keyList = keys.join(',')

  // The order changed: note where every row is now, before the newly sorted rows replace them.
  useLayoutEffect(() => {
    if (shownOrder.current === order || !container.current) return
    shownOrder.current = order
    before.current = positions(container.current)
  }, [order]) // oxlint-disable-line react-hooks/exhaustive-deps

  // New rows on screen: if they came from a change of order, slide the ones that were already there.
  useLayoutEffect(() => {
    const element = container.current
    const from = before.current
    if (!element || !from) return
    const now = positions(element)
    // The same rows in the same places: the order's rows haven't arrived yet.
    if (sameKeys(from, now)) return
    before.current = null
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

    let index = 0
    for (const row of element.querySelectorAll<HTMLElement>('[data-flip]')) {
      const was = from.get(row.dataset.flip!)
      const is = now.get(row.dataset.flip!)
      if (was === undefined || is === undefined || was === is) continue
      const delay = Math.min(index++ * STAGGER_MS, MAX_STAGGER_MS)
      // While moving, a row is opaque (rows cross each other) and the one going further is on top.
      Object.assign(row.style, {
        transition: 'none',
        transform: `translateY(${was - is}px)`,
        position: 'relative',
        zIndex: String(1 + Math.round(Math.abs(was - is) / 100)),
        background: 'var(--z-surface)',
      })
      // Read layout so the starting place is applied before the transition back to none.
      void row.offsetHeight
      row.style.transition = `transform ${DURATION_MS}ms cubic-bezier(0.2, 0.8, 0.3, 1) ${delay}ms`
      row.style.transform = ''
      row.addEventListener(
        'transitionend',
        () => Object.assign(row.style, { transition: '', position: '', zIndex: '', background: '' }),
        { once: true },
      )
    }
  }, [keyList]) // oxlint-disable-line react-hooks/exhaustive-deps
}

function positions(element: HTMLElement): Map<string, number> {
  const map = new Map<string, number>()
  for (const row of element.querySelectorAll<HTMLElement>('[data-flip]')) {
    map.set(row.dataset.flip!, row.getBoundingClientRect().top)
  }
  return map
}

function sameKeys(a: Map<string, number>, b: Map<string, number>) {
  if (a.size !== b.size) return false
  const ak = [...a.keys()]
  const bk = [...b.keys()]
  return ak.every((key, i) => key === bk[i])
}
