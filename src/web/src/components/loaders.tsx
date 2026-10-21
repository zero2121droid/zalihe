import { Box, Center, CloseButton, Stack, Text } from '@mantine/core'
import { useWindowEvent } from '@mantine/hooks'
import type { CSSProperties, HTMLAttributes, ReactNode, Ref } from 'react'
import { useTranslation } from 'react-i18next'
import classes from './loaders.module.css'
import { MARK_BODY, MARK_LID } from './logoMark'

const cx = (...names: Array<string | false | undefined>) => names.filter(Boolean).join(' ')

/**
 * The default Mantine loader (registered in theme.ts), so every `<Loader>` and `<Button loading>`
 * shows it. Mantine passes its size and color through `--loader-size` and `--loader-color`.
 */
export function ShelfLoader({ className, ref, ...others }: HTMLAttributes<HTMLSpanElement> & { ref?: Ref<HTMLSpanElement> }) {
  return (
    <span ref={ref} className={cx(classes.shelf, className)} {...others}>
      <svg viewBox="0 0 48 48" aria-hidden>
        <rect className={classes.bar} x="6" y="6" width="9" height="36" rx="1" />
        <rect className={classes.bar} x="20" y="6" width="9" height="36" rx="1" />
        <rect className={classes.bar} x="34" y="6" width="9" height="36" rx="1" />
      </svg>
    </span>
  )
}

/** Size of the page and box loaders on screen; both are drawn on the same 48-unit grid. */
const LOADER_SIZE = 120

/**
 * Where a large loader sits: in the middle of the visible content area (`screen`, the default:
 * right of the navigation, below the phone header, whatever the page shows above it; the whole
 * screen before the app layout exists), or just in its own box (`inline`).
 */
type LoaderPlacement = 'screen' | 'inline'

/** Both large loaders draw on y 13-42 of the 48-unit grid; this viewBox puts that in the middle. */
const CENTERED_VIEW_BOX = '0 3.5 48 48'

/** The logo's box (16-unit grid); `lidClassName` animates the lid on its own. */
function LogoBox({ lidClassName }: { lidClassName?: string }) {
  return (
    <>
      <path className={lidClassName} d={MARK_LID} />
      <path d={MARK_BODY} fillRule="evenodd" />
    </>
  )
}

/** A large loader in the middle of its area, with its label under it. */
function CenteredLoader({ label, placement, children }: { label?: string; placement: LoaderPlacement; children: ReactNode }) {
  const { t } = useTranslation()
  const content = (
    <Stack gap={16} align="center" role="status">
      {children}
      <Text fz="sm" c="dimmed">
        {label ?? t('common.loading')}
      </Text>
    </Stack>
  )
  return placement === 'inline' ? <Center py="xl">{content}</Center> : <div className={classes.screen}>{content}</div>
}

/** One of the stacking boxes: the logo's box, 16 units wide, its lid's top at `y`. */
function StackBox({ x, y }: { x: number; y: number }) {
  // The animation moves the outer group with a CSS transform, which would replace a transform
  // attribute on the same element, so the position sits on an inner group.
  return (
    <g className={classes.stackBox}>
      <g transform={`translate(${x} ${y - 1})`}>
        <LogoBox />
      </g>
    </g>
  )
}

/** Loading a whole page or section: boxes with lids stacking up. */
export function PageLoader({ label, placement = 'screen' }: { label?: string; placement?: LoaderPlacement }) {
  return (
    <CenteredLoader label={label} placement={placement}>
      <svg className={cx(classes.stack, classes.large)} width={LOADER_SIZE} height={LOADER_SIZE} viewBox={CENTERED_VIEW_BOX} aria-hidden>
        <StackBox x={7} y={28} />
        <StackBox x={25} y={28} />
        <StackBox x={16} y={13} />
      </svg>
    </CenteredLoader>
  )
}

function BoxIcon({ size, calm, large }: { size: number; calm?: boolean; large?: boolean }) {
  return (
    <svg
      className={cx(classes.box, calm && classes.calm, large && classes.large)}
      width={size}
      height={size}
      viewBox={large ? CENTERED_VIEW_BOX : '0 0 48 48'}
      aria-hidden
    >
      {/* Between the eyes, so it doesn't show through them while it waits inside the box. */}
      {!calm && <rect className={classes.peek} x="21" y="20" width="6" height="9" />}
      <g transform="translate(8 11) scale(2)">
        <LogoBox lidClassName={classes.lid} />
      </g>
    </svg>
  )
}

/**
 * Opening one thing (an item, the shop's product list): a box opens and an item peeks out.
 * Whole pages use PageLoader instead; both are the same size on screen.
 */
export function BoxLoader({ label, placement = 'screen' }: { label?: string; placement?: LoaderPlacement }) {
  return (
    <CenteredLoader label={label} placement={placement}>
      <BoxIcon size={LOADER_SIZE} large />
    </CenteredLoader>
  )
}

/** Icon for empty states: a closed box whose lid lifts a little now and then. */
export function EmptyBox() {
  // 64 px: the same box as BoxLoader, still an icon next to the text (DESIGN.md forbids illustrations).
  return <BoxIcon size={64} calm />
}

/** Placeholder rows while a table loads. */
export function SkeletonRows({ rows = 5 }: { rows?: number }) {
  const { t } = useTranslation()
  return (
    <Box role="status" aria-label={t('common.loading')}>
      {Array.from({ length: rows }, (_, row) => (
        <div key={row} className={classes.skeletonRow} style={{ '--row': row } as CSSProperties} aria-hidden>
          <i />
          <i />
          <i />
        </div>
      ))}
    </Box>
  )
}

interface ImportOverlayProps {
  title: string
  note?: string
  /** False stops the packets once the work is done; the resting frame stays. */
  running?: boolean
  /** Shows an X in the corner (Escape works too) that closes the overlay. */
  onClose?: () => void
  closeLabel?: string
  children?: ReactNode
}

/**
 * Shown over a list while it is being imported: the import animation and its text on a section in
 * the middle of the visible content area. The page dims the list behind it (DESIGN.md: no blur).
 */
export function ImportOverlay({ title, note, running = true, onClose, closeLabel, children }: ImportOverlayProps) {
  useWindowEvent('keydown', (event) => {
    if (onClose && event.key === 'Escape') onClose()
  })
  return (
    <div className={classes.screen}>
      <Stack
        className={classes.panel}
        gap="xs"
        align="center"
        role="status"
        p="xl"
        w="min(480px, calc(100% - 32px))"
        bg="var(--z-surface)"
        bd="1px solid var(--z-line)"
        style={{ borderRadius: 'var(--mantine-radius-md)' }}
      >
        {onClose && <CloseButton className={classes.close} onClick={onClose} aria-label={closeLabel} autoFocus />}
        <ShopImportAnimation running={running} />
        <Text fw={500}>{title}</Text>
        {note && (
          <Text fz="sm" c="dimmed" ta="center">
            {note}
          </Text>
        )}
        {children}
      </Stack>
    </div>
  )
}

/** Products moving from the shop into stock, shown while an import runs. */
export function ShopImportAnimation({ running = true }: { running?: boolean }) {
  return (
    <svg className={cx(classes.import, !running && classes.stopped)} viewBox="0 0 320 72" aria-hidden>
      <polyline className={classes.line} points="14,28 22,12 58,12 66,28" />
      <rect className={classes.line} x="18" y="28" width="44" height="32" rx="2" />
      <rect className={classes.line} x="35" y="42" width="10" height="18" rx="1" />
      <line className={cx(classes.line, classes.path)} x1="78" y1="44" x2="244" y2="44" />
      {[0, 1, 2].map((packet) => (
        <g key={packet} className={classes.packet}>
          <g transform="translate(74 39.3) scale(0.625)">
            <LogoBox />
          </g>
        </g>
      ))}
      <g className={classes.warehouse}>
        <polyline className={classes.line} points="254,30 280,14 306,30" />
        <rect className={classes.line} x="258" y="30" width="44" height="30" rx="2" />
        <line className={classes.line} x1="264" y1="41" x2="296" y2="41" />
        <line className={classes.line} x1="264" y1="51" x2="296" y2="51" />
      </g>
    </svg>
  )
}
