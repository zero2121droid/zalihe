import { TextInput, type TextInputProps } from '@mantine/core'

interface DecimalInputProps extends Omit<TextInputProps, 'value' | 'onChange'> {
  value: string
  onChange: (value: string) => void
}

/**
 * Text field for decimals. Keeps what the user typed ("12,5" or "12.5") and leaves parsing
 * to submit time via `readDecimal` from lib/format, so typing a comma is never blocked or rewritten.
 */
export function DecimalInput({ value, onChange, ...props }: DecimalInputProps) {
  return (
    <TextInput
      {...props}
      inputMode="decimal"
      autoComplete="off"
      value={value}
      onChange={(e) => onChange(e.currentTarget.value)}
      styles={{ input: { fontFamily: 'var(--mantine-font-family-monospace)', fontVariantNumeric: 'tabular-nums' } }}
    />
  )
}

