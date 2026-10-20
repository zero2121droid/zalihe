import { type MouseEvent, type PointerEvent, useEffect, useRef } from 'react'

/** After this long held down, the step repeats; then every REPEAT_MS. */
const HOLD_MS = 450
const REPEAT_MS = 110

/**
 * Handlers for a step button (− / +) that steps once on press and keeps stepping while it's held,
 * like a counter. A keyboard press (Enter, Space) is a single step.
 */
export function useHoldRepeat(step: () => void) {
  const timers = useRef<{ hold?: ReturnType<typeof setTimeout>; repeat?: ReturnType<typeof setInterval> }>({})
  // The latest step, so a repeat that is already running uses the current value.
  const latest = useRef(step)
  useEffect(() => {
    latest.current = step
  })

  function stop() {
    clearTimeout(timers.current.hold)
    clearInterval(timers.current.repeat)
  }
  useEffect(() => stop, [])

  return {
    onPointerDown: (event: PointerEvent) => {
      if (event.button !== 0) return
      latest.current()
      stop()
      timers.current.hold = setTimeout(() => {
        timers.current.repeat = setInterval(() => latest.current(), REPEAT_MS)
      }, HOLD_MS)
    },
    onPointerUp: stop,
    onPointerLeave: stop,
    onPointerCancel: stop,
    // A click that a pointer started has already stepped on pointer down; detail 0 means a keyboard.
    onClick: (event: MouseEvent) => {
      if (event.detail === 0) latest.current()
    },
  }
}
