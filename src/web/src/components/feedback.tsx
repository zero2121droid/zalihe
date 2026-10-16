import { Button, type ButtonProps } from '@mantine/core'
import type { CSSProperties, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import classes from './feedback.module.css'

type SaveButtonProps = Omit<ButtonProps, 'children'> & {
  type?: 'submit' | 'button'
  onClick?: () => void
  /** True for a moment after saving (useSaved): the button shows a check and "Saved". */
  saved: boolean
  children: ReactNode
}

// In the saved state the button turns quiet (selected background, accent text) instead of primary.
const savedStyle = {
  '--button-bg': 'var(--z-selected)',
  '--button-hover': 'var(--z-selected)',
  '--button-color': 'var(--z-accent-text)',
} as CSSProperties

/** Submit button that confirms a save: shelf loader while saving, then a check that draws itself. */
export function SaveButton({ saved, children, ...props }: SaveButtonProps) {
  const { t } = useTranslation()
  return (
    <Button
      {...props}
      style={saved ? savedStyle : undefined}
      // A mutation stays pending until its onSuccess (which shows "Saved") returns.
      loading={saved ? false : props.loading}
      leftSection={
        saved ? (
          <svg className={classes.check} viewBox="0 0 16 16" aria-hidden>
            <polyline points="3,8.5 6.5,12 13,4.5" />
          </svg>
        ) : undefined
      }
      // While "Saved" shows, another click must not submit again.
      onClick={saved ? undefined : props.onClick}
      type={saved ? 'button' : props.type}
    >
      <span aria-live="polite">{saved ? t('common.saved') : children}</span>
    </Button>
  )
}
