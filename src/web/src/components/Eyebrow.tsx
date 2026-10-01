import { Text } from '@mantine/core'
import type { ReactNode } from 'react'

/** Small monospace uppercase label above titles and groups (DESIGN.md: "oznaka sekcije"). */
export function Eyebrow({ children }: { children: ReactNode }) {
  return (
    <Text component="span" ff="monospace" fz={11} lts="0.12em" tt="uppercase" c="dimmed">
      {children}
    </Text>
  )
}
