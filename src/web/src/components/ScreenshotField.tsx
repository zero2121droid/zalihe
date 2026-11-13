import { ActionIcon, Anchor, Box, Group, Loader, Stack, Text } from '@mantine/core'
import { IconPhotoPlus, IconX } from '@tabler/icons-react'
import { useEffect, useMemo, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import type { Screenshot } from '../lib/useScreenshot'
import classes from './FeedbackModal.module.css'

/** What a phone or computer hands over; shrinkImage turns what the browser can read into WebP or JPEG. */
const ACCEPT = 'image/jpeg,image/png,image/webp,image/heic,image/heif'

/** "⌘V" on a Mac, "Ctrl+V" elsewhere; phones have no keyboard shortcut, so the hint is hidden there. */
const pasteShortcut = /Mac|iPhone|iPad/.test(navigator.platform) ? '⌘V' : 'Ctrl+V'

/** "Add a screenshot" with a hint about pasting, or the picked picture with a button to remove it. */
export function ScreenshotField({ screenshot, error }: { screenshot: Screenshot; error?: string }) {
  const { t } = useTranslation()
  const input = useRef<HTMLInputElement>(null)
  const preview = usePreviewUrl(screenshot.value)
  const message = screenshot.unreadable ? t('feedback.screenshot.unreadable') : error

  return (
    <Stack gap={6}>
      <input
        ref={input}
        type="file"
        accept={ACCEPT}
        hidden
        aria-hidden
        tabIndex={-1}
        onChange={(e) => {
          void screenshot.pick(e.currentTarget.files?.[0])
          // The same file can be picked again after removing it.
          e.currentTarget.value = ''
        }}
      />
      {preview ? (
        <Group gap="sm" align="flex-start" wrap="nowrap">
          <Box className={classes.preview}>
            <img src={preview} alt={t('feedback.screenshot.preview')} />
          </Box>
          <ActionIcon variant="default" aria-label={t('feedback.screenshot.remove')} onClick={screenshot.clear}>
            <IconX size={16} stroke={1.8} />
          </ActionIcon>
        </Group>
      ) : (
        <Group gap={8} wrap="nowrap">
          {screenshot.shrinking ? (
            <Loader size={16} role="status" aria-label={t('feedback.screenshot.preparing')} />
          ) : (
            <IconPhotoPlus size={16} stroke={1.8} className={classes.kindIcon} data-active />
          )}
          <Text fz="sm" c="var(--z-text-2)">
            <Anchor component="button" type="button" fz="sm" disabled={screenshot.shrinking} onClick={() => input.current?.click()}>
              {t('feedback.screenshot.add')}
            </Anchor>{' '}
            <span className={classes.pasteHint}>{t('feedback.screenshot.paste', { shortcut: pasteShortcut })}</span>{' '}
            <Text span fz="sm" c="dimmed">
              {t('feedback.screenshot.optional')}
            </Text>
          </Text>
        </Group>
      )}
      {message && (
        <Text role="alert" fz="xs" c="var(--mantine-color-error)">
          {message}
        </Text>
      )}
    </Stack>
  )
}

/** An object URL for showing the picked picture, released when it changes or the field goes away. */
function usePreviewUrl(blob: Blob | null) {
  const url = useMemo(() => (blob ? URL.createObjectURL(blob) : null), [blob])
  useEffect(() => (url ? () => URL.revokeObjectURL(url) : undefined), [url])
  return url
}
