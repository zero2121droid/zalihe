import {
  ActionIcon,
  Anchor,
  Badge,
  Button,
  type CSSVariablesResolver,
  createTheme,
  Input,
  type MantineColorsTuple,
  Modal,
  NavLink,
  SegmentedControl,
  Table,
} from '@mantine/core'

/**
 * Design tokens from docs/DESIGN.md, one set per color scheme. Components use these CSS
 * variables (e.g. `var(--z-surface)`) or Mantine props, never hex values.
 */
interface Tokens {
  bg: string
  sidebar: string
  surface: string
  surface2: string
  selected: string
  line: string
  lineSoft: string
  lineStrong: string
  text: string
  text2: string
  muted: string
  placeholder: string
  accent: string
  accentHover: string
  accentText: string
  linkHover: string
  onAccent: string
  statusOk: string
  statusLow: string
  statusOut: string
  incoming: string
}

const dark: Tokens = {
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
  placeholder: '#7E8473',
  accent: '#B4C17E',
  accentHover: '#CBD69A',
  accentText: '#B4C17E',
  linkHover: '#CBD69A',
  onAccent: '#11130E',
  statusOk: '#9DB06A',
  statusLow: '#E0A84A',
  statusOut: '#E8806A',
  incoming: '#B4C17E',
}

const light: Tokens = {
  bg: '#F5F5EF',
  sidebar: '#EDEEE6',
  surface: '#FFFFFF',
  surface2: '#E9EBE1',
  selected: '#E1E7CF',
  line: '#D9DCCE',
  lineSoft: '#E7E9DF',
  lineStrong: '#BFC4B0',
  text: '#1B1E16',
  text2: '#3B4033',
  muted: '#5C6250',
  placeholder: '#7A806E',
  accent: '#B4C17E',
  accentHover: '#A3B16A',
  accentText: '#55621E',
  linkHover: '#1B1E16',
  onAccent: '#11130E',
  statusOk: '#4C6A1C',
  statusLow: '#94600A',
  statusOut: '#B03A22',
  incoming: '#55621E',
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
  dark.placeholder,
  dark.lineStrong,
  dark.line,
  dark.surface,
  dark.bg,
  '#0E100B',
  '#0A0C08',
]

function schemeVariables(t: Tokens): Record<string, string> {
  return {
    '--z-bg': t.bg,
    '--z-sidebar': t.sidebar,
    '--z-surface': t.surface,
    '--z-surface-2': t.surface2,
    '--z-selected': t.selected,
    '--z-line': t.line,
    '--z-line-soft': t.lineSoft,
    '--z-line-strong': t.lineStrong,
    '--z-text': t.text,
    '--z-text-2': t.text2,
    '--z-muted': t.muted,
    '--z-accent': t.accent,
    '--z-accent-hover': t.accentHover,
    '--z-accent-text': t.accentText,
    '--z-link-hover': t.linkHover,
    '--z-on-accent': t.onAccent,
    '--z-status-ok': t.statusOk,
    '--z-status-low': t.statusLow,
    '--z-status-out': t.statusOut,
    '--z-incoming': t.incoming,

    // Mantine's own variables point at the tokens, so built-in components follow the design.
    '--mantine-color-body': t.bg,
    '--mantine-color-text': t.text,
    '--mantine-color-bright': t.text,
    '--mantine-color-dimmed': t.muted,
    '--mantine-color-placeholder': t.placeholder,
    '--mantine-color-anchor': t.accentText,
    '--mantine-color-error': t.statusOut,
    '--mantine-color-default': 'transparent',
    '--mantine-color-default-hover': t.surface2,
    '--mantine-color-default-color': t.text,
    '--mantine-color-default-border': t.lineStrong,
    '--mantine-color-disabled': t.surface2,
    '--mantine-color-disabled-color': t.placeholder,
    '--mantine-color-disabled-border': t.line,
  }
}

export const cssVariablesResolver: CSSVariablesResolver = () => ({
  variables: {},
  light: schemeVariables(light),
  dark: schemeVariables(dark),
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
  focusClassName: 'z-focus',
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
            '--button-color': 'var(--z-on-accent)',
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
    Modal: Modal.extend({
      defaultProps: { radius: 'md', shadow: 'none' },
      styles: {
        content: { backgroundColor: 'var(--z-surface)', border: '1px solid var(--z-line)' },
        header: { backgroundColor: 'var(--z-surface)' },
        title: { fontSize: '15px', fontWeight: 600 },
      },
    }),
    SegmentedControl: SegmentedControl.extend({
      styles: {
        root: { backgroundColor: 'var(--z-surface-2)' },
        indicator: { backgroundColor: 'var(--z-selected)', boxShadow: 'none' },
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
