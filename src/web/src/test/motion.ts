import { vi } from 'vitest'

/**
 * Makes the page render as with "reduce motion" on until the test ends, so animated numbers show
 * their final value at once. For tests that check the end state, not the animation: waiting for a
 * real-time animation fails when the machine is busy.
 */
export function withReducedMotion() {
  const original = window.matchMedia
  vi.spyOn(window, 'matchMedia').mockImplementation((query: string) => ({
    ...original(query),
    matches: query.includes('prefers-reduced-motion'),
  }))
}
