import { Table, type TableTrProps } from '@mantine/core'
import type { ReactNode } from 'react'
import classes from './search.module.css'

const cx = (...names: Array<string | false | undefined>) => names.filter(Boolean).join(' ')

/** The text with the first place the typed text appears in it marked. */
export function SearchMatch({ text, term }: { text: string; term: string }) {
  const needle = term.trim().toLowerCase()
  const at = needle ? text.toLowerCase().indexOf(needle) : -1
  if (at < 0) return text
  return (
    <>
      {text.slice(0, at)}
      <mark className={classes.match}>{text.slice(at, at + needle.length)}</mark>
      {text.slice(at + needle.length)}
    </>
  )
}

/** Table body whose rows fold while the search text changes (`narrowing`: typing). */
export function SearchBody({ narrowing, children }: { narrowing: boolean; children: ReactNode }) {
  return <Table.Tbody className={narrowing ? classes.narrowing : undefined}>{children}</Table.Tbody>
}

/** Table row that folds away when `hidden`; its cells wrap their content in SearchCell. */
export function SearchRow({ hidden, className, ...props }: TableTrProps & { hidden: boolean }) {
  return <Table.Tr {...props} className={cx(className, classes.row, hidden && classes.hidden)} aria-hidden={hidden || undefined} />
}

export function SearchCell({ children }: { children: ReactNode }) {
  return <div className={classes.cell}>{children}</div>
}
