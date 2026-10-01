import { UnstyledButton } from '@mantine/core'
import type { ReactNode } from 'react'

/** Filter pill from the mockup: 32 px high, 16 px radius, accent border when selected. */
export function FilterPill({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <UnstyledButton
      onClick={onClick}
      aria-pressed={active}
      className="z-focus"
      h={32}
      px={12}
      fz="sm"
      c={active ? 'var(--z-text)' : 'var(--z-text-2)'}
      bg={active ? 'var(--z-selected)' : 'transparent'}
      style={{
        border: `1px solid ${active ? 'var(--z-accent)' : 'var(--z-line)'}`,
        borderRadius: 'var(--mantine-radius-lg)',
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
      }}
    >
      {children}
    </UnstyledButton>
  )
}
