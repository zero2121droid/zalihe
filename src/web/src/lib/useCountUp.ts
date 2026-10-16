import { useReducedMotion } from '@mantine/hooks'
import { useEffect, useState } from 'react'

/** About 30 ms per number, so a count is followed one by one, but never shorter than 0.6 s or longer than 3 s. */
const MS_PER_STEP = 30
const MIN_MS = 600
const MAX_MS = 3000

export function countUpDuration(target: number): number {
  return Math.min(MAX_MS, Math.max(MIN_MS, target * MS_PER_STEP))
}

/**
 * Counts whole numbers from 0 up to `target` once it is known (null: not yet), at an even pace.
 * With reduced motion the target shows at once. `finished` is true when the last number shows.
 */
export function useCountUp(target: number | null): { value: number; finished: boolean } {
  const reduceMotion = useReducedMotion()
  const [value, setValue] = useState(0)

  useEffect(() => {
    if (target === null) return
    const duration = countUpDuration(target)
    const start = performance.now()
    let frame = 0
    // State is set only from animation frames, never synchronously in the effect.
    const step = (now: number) => {
      const progress = reduceMotion ? 1 : Math.min(1, (now - start) / duration)
      setValue(Math.floor(target * progress))
      if (progress < 1) frame = requestAnimationFrame(step)
    }
    frame = requestAnimationFrame(step)
    return () => cancelAnimationFrame(frame)
  }, [target, reduceMotion])

  return { value, finished: target !== null && value === target }
}
