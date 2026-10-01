import {
  ActionIcon,
  Anchor,
  Badge,
  Button,
  type CSSVariablesResolver,
  createTheme,
  Input,
  type MantineColorsTuple,
  NavLink,
  Table,
} from '@mantine/core'

/**
 * Design tokens from docs/DESIGN.md. Components use these CSS variables (e.g. `var(--z-surface)`)
 * or Mantine props, never hex values. Only the dark scheme exists in v1; a light scheme
 * is added later by filling the `light` block in `cssVariablesResolver`.
 */
const dark = {
  bg: '#11130E',
  sidebar: '#141710',
  surface: '#191C15',
  surface2: '#20241B',
  selected: '#252B1C',
  line: '#2C3125',
  lineSoft: '#23271E',
  lineStrong: '#3A4030',
  text: '#E4E7DC',
  text2: '#C4C9B8',
  muted: '#9BA18F',
  accent: '#B4C17E',
  accentHover: '#CBD69A',
  statusOk: '#9DB06A',
  statusLow: '#E0A84A',
  statusOut: '#E8806A',
  incoming: '#B4C17E',
}

// Primary palette built around the accent (index 5) and its hover (index 3).
const olive: MantineColorsTuple = [
  '#F4F6EA',
  '#E8EDD4',
  '#DCE3BE',
  dark.accentHover,
  '#BFCB8C',
  dark.accent,
  '#9DAA68',
  '#869254',
  '#6E7942',
  '#576031',
]

// Mantine derives many dark-scheme defaults from this palette (0 = text ... 7 = body).
const darkPalette: MantineColorsTuple = [
  dark.text,
  dark.text2,
  dark.muted,
  '#7E8473',
  dark.lineStrong,
  dark.line,
  dark.surface,
  dark.bg,
  '#0E100B',
  '#0A0C08',
]

export const cssVariablesResolver: CSSVariablesResolver = () => ({
  variables: {},
  light: {},
  dark: {
    '--z-bg': dark.bg,
    '--z-sidebar': dark.sidebar,
    '--z-surface': dark.surface,
    '--z-surface-2': dark.surface2,
    '--z-selected': dark.selected,
    '--z-line': dark.line,
    '--z-line-soft': dark.lineSoft,
    '--z-line-strong': dark.lineStrong,
    '--z-text': dark.text,
    '--z-text-2': dark.text2,
    '--z-muted': dark.muted,
    '--z-accent': dark.accent,
    '--z-accent-hover': dark.accentHover,
    '--z-status-ok': dark.statusOk,
    '--z-status-low': dark.statusLow,
    '--z-status-out': dark.statusOut,
    '--z-incoming': dark.incoming,

    '--mantine-color-body': dark.bg,
    '--mantine-color-text': dark.text,
    '--mantine-color-bright': dark.text,
    '--mantine-color-dimmed': dark.muted,
    '--mantine-color-placeholder': '#7E8473',
    '--mantine-color-anchor': dark.accent,
    '--mantine-color-error': dark.statusOut,
    '--mantine-color-default': 'transparent',
    '--mantine-color-default-hover': dark.surface2,
    '--mantine-color-default-color': dark.text,
    '--mantine-color-default-border': dark.lineStrong,
  },
})

export const theme = createTheme({
  primaryColor: 'olive',
  primaryShade: 5,
  colors: { olive, dark: darkPalette },

  fontFamily: "'IBM Plex Sans', 'Segoe UI', system-ui, sans-serif",
  fontFamilyMonospace: "'IBM Plex Mono', ui-monospace, monospace",
  fontSizes: { xs: '12px', sm: '13px', md: '14px', lg: '15px', xl: '18px' },
  lineHeights: { md: '1.45' },
  headings: {
    fontFamily: "'IBM Plex Sans', 'Segoe UI', system-ui, sans-serif",
    fontWeight: '600',
    sizes: {
      h1: { fontSize: '26px', lineHeight: '1.2' },
      h2: { fontSize: '15px', lineHeight: '1.3' },
    },
  },

  // Spacing follows the 4 px scale from DESIGN.md.
  spacing: { xs: '8px', sm: '12px', md: '16px', lg: '24px', xl: '36px' },
  radius: { xs: '2px', sm: '4px', md: '6px', lg: '16px', xl: '16px' },
  defaultRadius: 'sm',
  shadows: { xs: 'none', sm: 'none', md: 'none', lg: 'none', xl: 'none' },
  focusRing: 'auto',
  cursorType: 'pointer',

  components: {
    Button: Button.extend({
      defaultProps: { size: 'md' },
      vars: (_theme, props) => ({
        root: {
          '--button-height': props.size === 'md' || !props.size ? '40px' : undefined,
          '--button-fz': '14px',
          '--button-padding-x': '16px',
          // Primary button: light olive with dark text, never white text (DESIGN.md).
          ...((props.variant ?? 'filled') === 'filled' && {
            '--button-color': 'var(--z-bg)',
            '--button-hover': 'var(--z-accent-hover)',
          }),
        },
      }),
      styles: { root: { fontWeight: 600 } },
    }),
    ActionIcon: ActionIcon.extend({
      defaultProps: { variant: 'subtle', color: 'gray' },
    }),
    Input: Input.extend({
      defaultProps: { size: 'md' },
      vars: () => ({ wrapper: { '--input-height': '40px', '--input-fz': '14px' } }),
      styles: {
        input: { backgroundColor: 'var(--z-surface)', borderColor: 'var(--z-line)' },
      },
    }),
    Anchor: Anchor.extend({
      defaultProps: { underline: 'never' },
    }),
    NavLink: NavLink.extend({
      styles: {
        root: { borderRadius: 'var(--mantine-radius-sm)', padding: '9px 10px' },
        label: { fontSize: '14px' },
      },
    }),
    Table: Table.extend({
      defaultProps: { verticalSpacing: 'sm', horizontalSpacing: 'lg' },
      styles: {
        table: { borderColor: 'var(--z-line-soft)' },
        th: {
          fontFamily: 'var(--mantine-font-family-monospace)',
          fontSize: '11px',
          fontWeight: 400,
          letterSpacing: '0.08em',
          textTransform: 'uppercase',
          color: 'var(--z-muted)',
        },
        tr: { borderColor: 'var(--z-line-soft)' },
      },
    }),
    Badge: Badge.extend({
      defaultProps: { variant: 'outline', radius: 'lg' },
      styles: { root: { textTransform: 'none', fontWeight: 400 } },
    }),
  },
})
