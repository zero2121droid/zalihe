/** "⌘K" on a Mac, "Ctrl K" elsewhere: the shortcut that opens the quick search. */
export const quickSearchShortcut = /Mac|iPhone|iPad/.test(navigator.platform) ? '⌘K' : 'Ctrl K'
