import { useReducedMotion } from '@mantine/hooks'
import { useEffect, useRef, useState } from 'react'

const DURATION_MS = 520

function decimals(value: number): number {
  return (String(value).split('.')[1] ?? '').length
}

/**
 * The value to show while a number glides to its new value (ease-out). Intermediate values keep
 * the precision of the numbers involved, so "12,5" doesn't flicker through "12,347".
 * With reduced motion the new value shows at once. `from` makes the first value glide in too
 * (e.g. 0 for figures that count up when a screen opens); by default it shows as is.
 */
export function useAnimatedNumber(value: number, from: number = value): number {
  const reduceMotion = useReducedMotion()
  const [shown, setShown] = useState(from)
  const shownRef = useRef(from)

  useEffect(() => {
    const from = shownRef.current
    if (reduceMotion || from === value) {
      shownRef.current = value
      setShown(value)
      return
    }

    const factor = 10 ** Math.max(decimals(from), decimals(value))
    const start = performance.now()
    let frame = 0
    const step = (now: number) => {
      const progress = Math.min(1, (now - start) / DURATION_MS)
      const eased = 1 - (1 - progress) ** 3
      const next = progress === 1 ? value : Math.round((from + (value - from) * eased) * factor) / factor
      shownRef.current = next
      setShown(next)
      if (progress < 1) frame = requestAnimationFrame(step)
    }
    frame = requestAnimationFrame(step)
    return () => cancelAnimationFrame(frame)
  }, [value, reduceMotion])

  return shown
}
