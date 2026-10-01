import { useMatches } from 'react-router'

/**
 * How wide a page's content may grow (DESIGN.md "Oblik i raspored"). The layout is fluid up to
 * this limit; text sizes never depend on screen width, zooming is left to the OS and browser.
 * - `wide`: screens with tables and overviews (home, items, import, reports), up to 1920 px
 * - `normal`: text and form pages (e.g. settings), up to 1180 px
 */
export type PageWidth = 'normal' | 'wide'

export interface RouteHandle {
  width?: PageWidth
}

/** The width declared by the deepest matched route (`handle: { width }` in router.tsx). */
export function usePageWidth(): PageWidth {
  const matches = useMatches()
  for (let i = matches.length - 1; i >= 0; i--) {
    const width = (matches[i].handle as RouteHandle | undefined)?.width
    if (width) return width
  }
  return 'normal'
}
