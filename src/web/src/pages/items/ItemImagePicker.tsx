import { Anchor, Box, Loader, Stack, Text } from '@mantine/core'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ItemImage } from '../../components/ItemImage'
import { shrinkImage, UnreadableImageError } from '../../lib/shrinkImage'

const SIZE = 88

/** Everything a phone or computer may hand over; shrinkImage turns what the browser can read into WebP or JPEG. */
const ACCEPT = 'image/jpeg,image/png,image/webp,image/heic,image/heif'

interface ItemImagePickerProps {
  url: string | null
  /** The picture is being saved or removed. */
  busy: boolean
  canRemove: boolean
  /** What went wrong after the picture was picked. */
  error?: string | null
  /** A new pick started, so older errors no longer apply. */
  onPickStart?: () => void
  /** Whether a picked picture is still being shrunk. */
  onShrinkingChange?: (shrinking: boolean) => void
  onPicked: (image: Blob) => void
  onRemove: () => void
}

/**
 * A picture with "Dodaj sliku" / "Promeni sliku" and, when there is one to remove, "Ukloni sliku".
 * The picture is shrunk in the browser, and what happens to it next is up to the parent.
 */
export function ItemImagePicker({
  url,
  busy,
  canRemove,
  error,
  onPickStart,
  onShrinkingChange,
  onPicked,
  onRemove,
}: ItemImagePickerProps) {
  const { t } = useTranslation()
  const input = useRef<HTMLInputElement>(null)
  const [shrinking, setShrinking] = useState(false)
  const [unreadable, setUnreadable] = useState(false)

  const working = shrinking || busy
  const message = unreadable ? t('items.image.unreadable') : error

  function setShrinkingBoth(value: boolean) {
    setShrinking(value)
    onShrinkingChange?.(value)
  }

  const onPick = async (file: File | undefined) => {
    if (!file) return
    setUnreadable(false)
    onPickStart?.()
    setShrinkingBoth(true)
    try {
      onPicked(await shrinkImage(file))
    } catch (e) {
      if (!(e instanceof UnreadableImageError)) throw e
      setUnreadable(true)
    } finally {
      setShrinkingBoth(false)
    }
  }

  return (
    <Stack gap={6} w={SIZE} style={{ flexShrink: 0 }}>
      <Box pos="relative">
        <ItemImage url={url} size={SIZE} />
        {working && (
          <Box pos="absolute" inset={0} bg="var(--z-surface)" style={{ display: 'grid', placeItems: 'center', opacity: 0.85 }}>
            <Loader size={20} role="status" aria-label={t('items.image.saving')} />
          </Box>
        )}
      </Box>
      <input
        ref={input}
        type="file"
        accept={ACCEPT}
        hidden
        aria-hidden
        tabIndex={-1}
        onChange={(e) => {
          void onPick(e.currentTarget.files?.[0])
          // The same file can be picked again after an error.
          e.currentTarget.value = ''
        }}
      />
      <Stack gap={2} align="flex-start">
        <Anchor component="button" type="button" fz="xs" disabled={working} onClick={() => input.current?.click()}>
          {url ? t('items.image.change') : t('items.image.add')}
        </Anchor>
        {canRemove && (
          <Anchor component="button" type="button" fz="xs" c="dimmed" disabled={working} onClick={onRemove}>
            {t('items.image.remove')}
          </Anchor>
        )}
        {message && (
          <Text role="alert" fz="xs" c="var(--mantine-color-error)" w={180}>
            {message}
          </Text>
        )}
      </Stack>
    </Stack>
  )
}
