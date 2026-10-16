import { useState } from 'react'
import classes from '../components/feedback.module.css'

/**
 * Shakes the fields with an error once, each time `shake()` is called (a failed submit). Put
 * `className` on the form; it switches between two identical animations so each call restarts it.
 */
export function useShake(): { className: string | undefined; shake: () => void } {
  const [count, setCount] = useState(0)
  return {
    className: count === 0 ? undefined : count % 2 ? classes.shakeA : classes.shakeB,
    shake: () => setCount((n) => n + 1),
  }
}
