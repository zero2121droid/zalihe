import { Anchor, Box, Loader, Stack, Text } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import { getGetItemQueryKey, getListItemsQueryKey, useRemoveItemImage, useSetItemImage } from '../../api/generated/items/items'
import type { ItemDto } from '../../api/generated/model'
import { ItemImage } from '../../components/ItemImage'
import { shrinkImage, UnreadableImageError } from '../../lib/shrinkImage'

const SIZE = 88

/** Everything a phone or computer may hand over; shrinkImage turns what the browser can read into WebP or JPEG. */
const ACCEPT = 'image/jpeg,image/png,image/webp,image/heic,image/heif'

/**
 * The item's picture on its page, with "Dodaj sliku" / "Promeni sliku" and, for a picture the user
 * added, "Ukloni sliku". The picture is shrunk in the browser before it is sent.
 */
export function ItemImageEditor({ item }: { item: ItemDto }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const [shrinking, setShrinking] = useState(false)
  const [unreadable, setUnreadable] = useState(false)

  // Without the picture the item may show its shop image again, which only the API knows.
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getGetItemQueryKey(item.id) }),
      queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() }),
    ])
  const upload = useSetItemImage({
    mutation: {
      // The answer is the updated item: show it at once and refresh the rest without waiting.
      onSuccess: (updated) => {
        queryClient.setQueryData(getGetItemQueryKey(item.id), updated)
        void queryClient.invalidateQueries({ queryKey: getListItemsQueryKey() })
      },
    },
  })
  const remove = useRemoveItemImage({ mutation: { onSuccess: refresh } })

  const busy = shrinking || upload.isPending || remove.isPending
  const error = unreadable
    ? t('items.image.unreadable')
    : (fieldErrorMessages(t, upload.error).file ?? formErrorMessage(t, upload.error) ?? formErrorMessage(t, remove.error))

  const onPick = async (file: File | undefined) => {
    if (!file) return
    setUnreadable(false)
    upload.reset()
    remove.reset()
    setShrinking(true)
    try {
      upload.mutate({ id: item.id, data: { file: await shrinkImage(file) } })
    } catch (e) {
      if (!(e instanceof UnreadableImageError)) throw e
      setUnreadable(true)
    } finally {
      setShrinking(false)
    }
  }

  return (
    <Stack gap={6} w={SIZE} style={{ flexShrink: 0 }}>
      <Box pos="relative">
        <ItemImage url={item.imageUrl} size={SIZE} />
        {busy && (
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
        <Anchor component="button" type="button" fz="xs" disabled={busy} onClick={() => input.current?.click()}>
          {item.imageUrl ? t('items.image.change') : t('items.image.add')}
        </Anchor>
        {item.hasOwnImage && (
          <Anchor component="button" type="button" fz="xs" c="dimmed" disabled={busy} onClick={() => remove.mutate({ id: item.id })}>
            {t('items.image.remove')}
          </Anchor>
        )}
        {error && (
          <Text role="alert" fz="xs" c="var(--mantine-color-error)" w={180}>
            {error}
          </Text>
        )}
      </Stack>
    </Stack>
  )
}
