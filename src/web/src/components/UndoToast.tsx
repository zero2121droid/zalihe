import { UnstyledButton } from '@mantine/core'
import classes from './UndoToast.module.css'

interface UndoToastProps {
  message: string
  undoLabel: string
  onUndo: () => void
  /** Called once the time to undo has run out. */
  onDone: () => void
}

/**
 * A message at the bottom of the screen after something left the screen (I from the animation
 * sheet), with a way back. The line under it shrinks over the five seconds left to undo; the time
 * stops while the pointer or keyboard focus is on the message, so reading it never costs the undo.
 */
export function UndoToast({ message, undoLabel, onUndo, onDone }: UndoToastProps) {
  return (
    <div className={classes.place}>
      <div className={classes.toast} role="status">
        <div className={classes.body}>
          <span className={classes.message}>{message}</span>
          <UnstyledButton className={classes.undo} onClick={onUndo}>
            {undoLabel}
          </UnstyledButton>
        </div>
        {/* The time runs as this line's animation, so pausing it pauses the countdown too. */}
        <div className={classes.timer} onAnimationEnd={onDone} />
      </div>
    </div>
  )
}
