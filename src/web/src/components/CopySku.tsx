import { CopyButton, UnstyledButton } from '@mantine/core'
import { IconCheck, IconCopy } from '@tabler/icons-react'
import { useTranslation } from 'react-i18next'
import classes from './CopySku.module.css'

/**
 * The SKU as a button that copies it (to paste into the shop or a supplier's order). The copy icon
 * turns into a check for a moment; the SKU stays, so nothing next to it moves.
 */
export function CopySku({ sku }: { sku: string }) {
  const { t } = useTranslation()
  return (
    <CopyButton value={sku} timeout={1600}>
      {({ copied, copy }) => (
        <UnstyledButton
          onClick={copy}
          className={`z-focus ${classes.copy}`}
          data-copied={copied || undefined}
          aria-label={t('items.copySku', { sku })}
        >
          <span className={classes.icons} aria-hidden>
            <IconCopy size={14} stroke={1.8} className={classes.copyIcon} />
            <IconCheck size={14} stroke={2.2} className={classes.checkIcon} />
          </span>
          {sku}
          <span className={classes.status} role="status">
            {copied ? t('items.copied') : ''}
          </span>
        </UnstyledButton>
      )}
    </CopyButton>
  )
}
