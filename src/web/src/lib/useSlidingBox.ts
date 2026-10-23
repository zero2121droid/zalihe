import { type RefObject, useLayoutEffect, useState } from 'react'

export interface SlidingBox {
  x: number
  y: number
  width: number
  height: number
}

/**
 * Where a sliding highlight goes: the box of the child of `container` that matches `selector` (the
 * selected pill, the current page), measured after every render and again whenever the container
 * changes size (fonts loading, wrapping onto two lines on a phone). `placed` turns true one frame after
 * the first position, so the highlight starts there instead of sliding in from the corner.
 */
export function useSlidingBox(container: RefObject<HTMLElement | null>, selector: string) {
  const [box, setBox] = useState<SlidingBox | null>(null)
  const [placed, setPlaced] = useState(false)

  useLayoutEffect(() => {
    const element = container.current
    if (!element) return
    const measure = () => {
      const target = element.querySelector<HTMLElement>(selector)
      const next = target
        ? { x: target.offsetLeft, y: target.offsetTop, width: target.offsetWidth, height: target.offsetHeight }
        : null
      // Measured after every render (a pill's count may change its width), so only a real move updates.
      setBox((current) =>
        current && next && Object.keys(next).every((key) => current[key as keyof SlidingBox] === next[key as keyof SlidingBox])
          ? current
          : next,
      )
    }
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(element)
    return () => observer.disconnect()
  })

  useLayoutEffect(() => {
    if (box && !placed) requestAnimationFrame(() => setPlaced(true))
  }, [box, placed])

  return { box, placed }
}
