import { type ReactNode, useState } from 'react'
import classes from './PageTurn.module.css'

/**
 * A list's rows that come in from the side the user paged to: from the right for a later page, from
 * the left for an earlier one (8 px, DESIGN.md "Pokret"). `page` is the page of the data shown (the
 * API's answer), not the one asked for, so the old rows don't move while the next page loads.
 */
export function PageTurn({ page, children }: { page: number; children: ReactNode }) {
  const [seen, setSeen] = useState(page)
  const [direction, setDirection] = useState<'next' | 'previous' | null>(null)

  if (seen !== page) {
    setSeen(page)
    setDirection(page > seen ? 'next' : 'previous')
  }

  return (
    <div key={page} className={direction ? classes[direction] : undefined}>
      {children}
    </div>
  )
}
