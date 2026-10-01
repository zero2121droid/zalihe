import { localStorageColorSchemeManager } from '@mantine/core'

/**
 * The theme choice is kept on the device (not on the account): a phone in the warehouse and a
 * laptop in the evening can use different themes. Dark is the default (DESIGN.md).
 * index.html reads the same key before React starts, so the page never flashes the wrong theme.
 */
export const COLOR_SCHEME_KEY = 'zalihe.colorScheme'
export const defaultColorScheme = 'dark'

export const colorSchemeManager = localStorageColorSchemeManager({ key: COLOR_SCHEME_KEY })
