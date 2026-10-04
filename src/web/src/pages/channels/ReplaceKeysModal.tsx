import { Button, Group, Modal, Stack, Text } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import { getListChannelsQueryKey, useReplaceChannelCredentials } from '../../api/generated/channels/channels'
import type { SalesChannelDto } from '../../api/generated/model'
import { KeyFields } from './KeyFields'
import { emptyKeys, type Keys } from './keys'

/** New API keys for a connected shop; they are saved only if the shop accepts them. */
export function ReplaceKeysModal({ channel, opened, onClose }: { channel: SalesChannelDto; opened: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [keys, setKeys] = useState<Keys>(emptyKeys)
  const replace = useReplaceChannelCredentials({
    mutation: {
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: getListChannelsQueryKey() })
        close()
      },
    },
  })

  function close() {
    setKeys(emptyKeys)
    replace.reset()
    onClose()
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    replace.mutate({ id: channel.id, data: keys })
  }

  const formError = formErrorMessage(t, replace.error)

  return (
    <Modal opened={opened} onClose={close} title={t('channels.replace.title')}>
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          <Text fz="sm" c="dimmed">
            {t('channels.replace.hint')}
          </Text>
          <KeyFields keys={keys} onChange={setKeys} errors={fieldErrorMessages(t, replace.error)} autoFocus />
          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}
          <Group justify="flex-end" gap="sm">
            <Button variant="default" onClick={close}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" loading={replace.isPending}>
              {t('channels.replace.submit')}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
