import { UnstyledButton } from '@mantine/core'
import { useHoldRepeat } from '../lib/useHoldRepeat'
import classes from './StepButton.module.css'

/** A − or + button next to a quantity: one step per press, repeating while held. */
export function StepButton({ label, sign, onStep }: { label: string; sign: '−' | '+'; onStep: () => void }) {
  const hold = useHoldRepeat(onStep)
  return (
    <UnstyledButton {...hold} aria-label={label} className={`z-focus ${classes.step}`}>
      {sign}
    </UnstyledButton>
  )
}
