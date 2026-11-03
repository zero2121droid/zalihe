import { Group, Text } from '@mantine/core'
import { useId } from 'react'
import { useTranslation } from 'react-i18next'
import classes from './Logo.module.css'
import { MARK_BODY_SOLID, MARK_EYES, MARK_LID } from './logoMark'

/**
 * How the box moves (DESIGN.md "Pokret", logo):
 * - `land`: once on mount the lid drops onto the box and the eyes open (the usual logo);
 * - `stack`: once on mount the box drops, then the lid, then the eyes open, and afterwards it now
 *   and then looks left and right and blinks (the large mark on the sign-in pages);
 * - `none`: still.
 * With either motion, hovering makes the lid hop and the eyes blink.
 */
export type LogoMotion = 'land' | 'stack' | 'none'

export function LogoMark({ size = 16, motion = 'none' }: { size?: number; motion?: LogoMotion }) {
  // The mask needs a document-wide id; useId's punctuation isn't safe inside url(#…).
  const maskId = `logo-eyes-${useId().replace(/[^a-zA-Z0-9]/g, '')}`
  return (
    <svg
      className={`${classes.mark} ${motion !== 'none' ? classes[motion] : ''}`}
      width={size}
      height={size}
      viewBox="0 0 16 16"
      fill="var(--z-accent-text)"
      aria-hidden
    >
      <mask id={maskId} maskUnits="userSpaceOnUse" x="0" y="0" width="16" height="16">
        <rect {...MARK_BODY_SOLID} fill="white" />
        <g className={classes.eyes}>
          {MARK_EYES.map((eye) => (
            <g key={eye.x} className={classes.blink}>
              <rect className={classes.eye} {...eye} fill="black" />
            </g>
          ))}
        </g>
      </mask>
      <g className={classes.hop}>
        <path className={classes.lid} d={MARK_LID} />
      </g>
      <g className={classes.body}>
        <rect {...MARK_BODY_SOLID} mask={`url(#${maskId})`} />
      </g>
    </svg>
  )
}

export function Logo({ motion = 'land' }: { motion?: LogoMotion }) {
  const { t } = useTranslation()
  return (
    <Group className={classes.logo} gap={10} wrap="nowrap">
      <LogoMark motion={motion} />
      <Text ff="monospace" fz={17} fw={600} lts="-0.02em">
        {t('common.appName')}
      </Text>
    </Group>
  )
}
