import { useEffect, useRef, useState } from 'react'

/** Wait this long before showing a loader: a fast answer shows no loader at all. */
export const LOADER_DELAY_MS = 200
/** Once shown, keep the loader at least this long, so it doesn't flash. */
export const LOADER_MIN_VISIBLE_MS = 500

/**
 * What a page shows for a loading request: `blank` during the first moments, then `loader`, then
 * `content`. Content can wait for the loader's minimum time even after the data has arrived.
 */
export type LoadingDisplay = 'blank' | 'loader' | 'content'

export function useLoadingDisplay(pending: boolean): LoadingDisplay {
  const [loaderShown, setLoaderShown] = useState(false)
  const shownAt = useRef(0)

  useEffect(() => {
    if (pending && !loaderShown) {
      const timer = setTimeout(() => {
        shownAt.current = Date.now()
        setLoaderShown(true)
      }, LOADER_DELAY_MS)
      return () => clearTimeout(timer)
    }
    if (!pending && loaderShown) {
      const left = LOADER_MIN_VISIBLE_MS - (Date.now() - shownAt.current)
      const timer = setTimeout(() => setLoaderShown(false), Math.max(0, left))
      return () => clearTimeout(timer)
    }
  }, [pending, loaderShown])

  if (loaderShown) return 'loader'
  return pending ? 'blank' : 'content'
}
