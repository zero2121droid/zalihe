import { useReducedMotion } from '@mantine/hooks'
import { useState } from 'react'

/** How long "Saved" stays on the button before the form closes. */
export const SAVED_HOLD_MS = 600

/**
 * The "Saved" moment after a successful save: `confirm()` shows it and resolves when it is over,
 * so the form closes after the user has seen it. With reduced motion it resolves at once.
 */
export function useSaved(): { saved: boolean; confirm: () => Promise<void> } {
  const reduceMotion = useReducedMotion()
  const [saved, setSaved] = useState(false)

  async function confirm() {
    if (reduceMotion) return
    setSaved(true)
    await new Promise((resolve) => setTimeout(resolve, SAVED_HOLD_MS))
    setSaved(false)
  }

  return { saved, confirm }
}
