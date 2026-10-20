import { vi } from 'vitest'

/** Makes the page render as on a phone: width queries (max-width) match until the test ends. */
export function onPhone() {
  const original = window.matchMedia
  vi.spyOn(window, 'matchMedia').mockImplementation((query: string) => ({
    ...original(query),
    matches: query.includes('max-width'),
  }))
}
