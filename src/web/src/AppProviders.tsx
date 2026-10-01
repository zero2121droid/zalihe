import { MantineProvider } from '@mantine/core'
import { type QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { type ReactNode, useState } from 'react'
import { colorSchemeManager, defaultColorScheme } from './colorScheme'
import { createQueryClient } from './queryClient'
import { cssVariablesResolver, theme } from './theme'

/** Everything the UI needs around it: theme and server state. Also used by tests. */
export function AppProviders({ children, queryClient }: { children: ReactNode; queryClient?: QueryClient }) {
  const [client] = useState(() => queryClient ?? createQueryClient())
  return (
    <MantineProvider
      theme={theme}
      cssVariablesResolver={cssVariablesResolver}
      colorSchemeManager={colorSchemeManager}
      defaultColorScheme={defaultColorScheme}
    >
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    </MantineProvider>
  )
}
