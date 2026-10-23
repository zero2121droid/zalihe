import { Anchor, Box, Button, Group, List, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { type ReactNode, type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import {
  getListChannelsQueryKey,
  useCheckChannel,
  useConnectWooCommerce,
  useListChannels,
} from '../../api/generated/channels/channels'
import type { SalesChannelDto } from '../../api/generated/model'
import { Eyebrow } from '../../components/Eyebrow'
import { PageLoader } from '../../components/loaders'
import { formatDateTime } from '../../lib/format'
import { useLoadingDisplay } from '../../lib/useLoadingDisplay'
import { ChannelStatus } from './ChannelStatus'
import { ChannelSummary } from './ChannelSummary'
import { KeyFields } from './KeyFields'
import { emptyKeys, type Keys } from './keys'
import { ReplaceKeysModal } from './ReplaceKeysModal'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

export function ChannelsPage() {
  const { t } = useTranslation()
  const channels = useListChannels()
  const display = useLoadingDisplay(channels.isPending)

  return (
    <Stack gap={28}>
      <Stack gap={4}>
        <Eyebrow>{t('channels.eyebrow')}</Eyebrow>
        <Title order={1} lts="-0.01em">
          {t('channels.title')}
        </Title>
      </Stack>

      {channels.isPending || display !== 'content' ? (
        display === 'loader' && <PageLoader />
      ) : channels.isError ? (
        <Text role="alert">{formErrorMessage(t, channels.error)}</Text>
      ) : channels.data.length === 0 ? (
        <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="lg" style={{ alignItems: 'start' }}>
          <ConnectForm />
          <KeysHelp />
        </SimpleGrid>
      ) : (
        <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="lg" style={{ alignItems: 'start' }}>
          {channels.data.map((channel) => (
            <ChannelCard key={channel.id} channel={channel} />
          ))}
        </SimpleGrid>
      )}
    </Stack>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Box component="section" aria-label={title} bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Box px="lg" py="md" style={{ borderBottom: '1px solid var(--z-line)' }}>
        <Title order={2}>{title}</Title>
      </Box>
      <Box p="lg">{children}</Box>
    </Box>
  )
}

function ConnectForm() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [baseUrl, setBaseUrl] = useState('')
  const [keys, setKeys] = useState<Keys>(emptyKeys)
  const connect = useConnectWooCommerce({
    mutation: { onSuccess: () => queryClient.invalidateQueries({ queryKey: getListChannelsQueryKey() }) },
  })

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    connect.mutate({ data: { baseUrl, ...keys } })
  }

  const errors = fieldErrorMessages(t, connect.error)
  const formError = formErrorMessage(t, connect.error)

  return (
    <Section title={t('channels.connect.title')}>
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          <Text fz="sm" c="dimmed">
            {t('channels.connect.intro')}
          </Text>
          <TextInput
            label={t('channels.form.baseUrl')}
            description={t('channels.form.baseUrlHint')}
            inputWrapperOrder={['label', 'input', 'description', 'error']}
            withAsterisk
            placeholder="https://mojaprodavnica.rs"
            inputMode="url"
            autoComplete="url"
            spellCheck={false}
            value={baseUrl}
            onChange={(e) => setBaseUrl(e.currentTarget.value)}
            error={errors.baseUrl}
          />
          <KeyFields keys={keys} onChange={setKeys} errors={errors} />
          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}
          <Group justify="flex-end">
            <Button type="submit" loading={connect.isPending}>
              {t('channels.connect.submit')}
            </Button>
          </Group>
        </Stack>
      </form>
    </Section>
  )
}

function KeysHelp() {
  const { t } = useTranslation()
  return (
    <Section title={t('channels.help.title')}>
      <Stack gap="md">
        <List type="ordered" spacing="sm" fz="sm">
          <List.Item>{t('channels.help.step1')}</List.Item>
          <List.Item>{t('channels.help.step2')}</List.Item>
          <List.Item>{t('channels.help.step3')}</List.Item>
        </List>
        <Text fz="sm" c="dimmed">
          {t('channels.help.safety')}
        </Text>
        <Text fz="sm" c="dimmed">
          {t('channels.privacy')}
        </Text>
      </Stack>
    </Section>
  )
}

function ChannelCard({ channel }: { channel: SalesChannelDto }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [keysOpened, { open: openKeys, close: closeKeys }] = useDisclosure()
  const [checkedAt, setCheckedAt] = useState<Date | null>(null)
  const check = useCheckChannel({
    mutation: {
      onSuccess: (updated) => {
        queryClient.setQueryData<SalesChannelDto[]>(getListChannelsQueryKey(), (list) =>
          list?.map((c) => (c.id === updated.id ? updated : c)),
        )
        setCheckedAt(new Date())
      },
    },
  })
  const host = new URL(channel.baseUrl).host
  const checkError = formErrorMessage(t, check.error)

  return (
    <Section title={t(`channels.types.${channel.type}`)}>
      <Stack gap="lg">
        <Stack gap={4}>
          <Anchor href={channel.baseUrl} target="_blank" rel="noreferrer" fz="lg" fw={500} c="var(--z-text)">
            {host}
          </Anchor>
          <Text fz="xs" c="dimmed">
            {t('channels.card.connectedSince', { date: formatDateTime(channel.createdAt, i18n.language) })}
          </Text>
        </Stack>

        <Stack gap={6}>
          <ChannelStatus status={channel.status} checking={check.isPending} fresh={check.isSuccess && channel.status === 'connected'} />
          {channel.status === 'error' && channel.lastErrorCode && (
            <Text fz="sm" c="var(--z-text-2)">
              {t(`errors.${channel.lastErrorCode}`, { defaultValue: t('errors.common.unexpected') })}
            </Text>
          )}
          {channel.status === 'error' && channel.failingSince && (
            <Text fz="xs" c="dimmed">
              {t('channels.card.failingSince', { date: formatDateTime(channel.failingSince, i18n.language) })}
            </Text>
          )}
          {checkedAt && (
            <Text fz="xs" c="dimmed">
              {t('channels.card.checkedAt', { date: formatDateTime(checkedAt, i18n.language) })}
            </Text>
          )}
          {checkError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {checkError}
            </Text>
          )}
        </Stack>

        <ChannelSummary channelId={channel.id} />

        <Group gap="sm" className="z-phone-stack">
          <Button component={Link} to={`/channels/${channel.id}/import`}>
            {t('channels.card.importProducts')}
          </Button>
          <Button variant="default" loading={check.isPending} onClick={() => check.mutate({ id: channel.id })}>
            {t('channels.card.check')}
          </Button>
          <Button variant="default" onClick={openKeys}>
            {t('channels.card.replaceKeys')}
          </Button>
        </Group>

        <Text fz="xs" c="dimmed">
          {t('channels.privacy')}
        </Text>
      </Stack>
      <ReplaceKeysModal channel={channel} opened={keysOpened} onClose={closeKeys} />
    </Section>
  )
}
