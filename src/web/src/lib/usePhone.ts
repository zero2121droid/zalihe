import { useMantineTheme } from '@mantine/core'
import { useMediaQuery } from '@mantine/hooks'

/**
 * True below the `sm` breakpoint, where the navigation folds into a menu and wide tables give way to
 * two columns (what it is, and its number). Read on the first render, so a phone never shows the
 * wide table for a moment.
 */
export function usePhone(): boolean {
  const { breakpoints } = useMantineTheme()
  const below = `${parseFloat(breakpoints.sm) - 0.01}em`
  return useMediaQuery(`(max-width: ${below})`, false, { getInitialValueInEffect: false }) ?? false
}

/**
 * True where the content area is too narrow for wide tables: below the `md` breakpoint by default, so
 * on a phone and also on a tablet, where the navigation still takes 232 px. Tables then use their
 * compact layout (fewer columns, the rest under the name) instead of scrolling sideways. A table that
 * needs more room passes a larger breakpoint (the items list, 900 px wide: `lg`).
 */
export function useCompactTables(breakpoint: 'md' | 'lg' = 'md'): boolean {
  const { breakpoints } = useMantineTheme()
  const below = `${parseFloat(breakpoints[breakpoint]) - 0.01}em`
  return useMediaQuery(`(max-width: ${below})`, false, { getInitialValueInEffect: false }) ?? false
}
