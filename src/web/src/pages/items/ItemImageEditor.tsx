import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import { getGetItemQueryKey, getListItemsQueryKey, useRemoveItemImage, useSetItemImage } from '../../api/generated/items/items'
import type { ItemDto } from '../../api/generated/model'
import { ItemImagePicker } from './ItemImagePicker'

/** The item's picture on its page, saved as soon as it is picked or removed. */
export function ItemImageEditor({ item }: { item: ItemDto }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

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

  return (
    <ItemImagePicker
      url={item.imageUrl}
      busy={upload.isPending || remove.isPending}
      canRemove={item.hasOwnImage}
      error={fieldErrorMessages(t, upload.error).file ?? formErrorMessage(t, upload.error) ?? formErrorMessage(t, remove.error)}
      onPickStart={() => {
        upload.reset()
        remove.reset()
      }}
      onPicked={(file) => upload.mutate({ id: item.id, data: { file } })}
      onRemove={() => remove.mutate({ id: item.id })}
    />
  )
}
