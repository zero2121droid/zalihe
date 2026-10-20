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
