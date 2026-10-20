import { Group, UnstyledButton } from '@mantine/core'
import { createContext, type CSSProperties, type ReactNode, use, useLayoutEffect, useRef, useState } from 'react'
import classes from './FilterPill.module.css'

interface Box {
  x: number
  y: number
  width: number
  height: number
}

/** True inside FilterPills, whose sliding highlight draws the selected pill's background and border. */
const SlidingGroup = createContext(false)

/** Filter pill from the mockup: 32 px high, 16 px radius, accent border when selected. */
export function FilterPill({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  const sliding = use(SlidingGroup)
  const selectedLook = active && !sliding
  return (
    <UnstyledButton
      onClick={onClick}
      aria-pressed={active}
      className={`z-focus ${classes.pill}`}
      h={32}
      px={12}
      fz="sm"
      c={active ? 'var(--z-text)' : 'var(--z-text-2)'}
      bg={selectedLook ? 'var(--z-selected)' : 'transparent'}
      style={{
        border: `1px solid ${selectedLook ? 'var(--z-accent)' : active ? 'transparent' : 'var(--z-line)'}`,
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

/**
 * A row of FilterPills where one is selected: the selection slides and stretches to the chosen
 * pill instead of jumping (DESIGN.md "Pokret"). It is measured from the pill marked aria-pressed,
 * again whenever the row changes size (fonts loading, wrapping onto two lines on a phone).
 */
export function FilterPills({ label, children }: { label: string; children: ReactNode }) {
  const row = useRef<HTMLDivElement>(null)
  const [box, setBox] = useState<Box | null>(null)
  // The first position is set without sliding there from the corner.
  const [placed, setPlaced] = useState(false)

  useLayoutEffect(() => {
    const element = row.current
    if (!element) return
    const measure = () => {
      const pill = element.querySelector<HTMLElement>('[aria-pressed="true"]')
      const next = pill ? { x: pill.offsetLeft, y: pill.offsetTop, width: pill.offsetWidth, height: pill.offsetHeight } : null
      // Measured after every render (a pill's count may change its width), so only a real move updates.
      setBox((current) =>
        current && next && Object.keys(next).every((key) => current[key as keyof Box] === next[key as keyof Box])
          ? current
          : next,
      )
    }
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(element)
    return () => observer.disconnect()
  })

  useLayoutEffect(() => {
    if (box && !placed) requestAnimationFrame(() => setPlaced(true))
  }, [box, placed])

  return (
    <SlidingGroup value>
      <Group ref={row} gap={6} role="group" aria-label={label} pos="relative">
        {box && (
          <span
            className={classes.highlight}
            data-placed={placed || undefined}
            style={
              {
                '--x': `${box.x}px`,
                '--y': `${box.y}px`,
                width: box.width,
                height: box.height,
              } as CSSProperties
            }
            aria-hidden
          />
        )}
        {children}
      </Group>
    </SlidingGroup>
  )
}
