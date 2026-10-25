import { Anchor, Box, Button, Group, List, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { type ReactNode, type SubmitEvent, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { fieldErrorMessages, formErrorMessage } from '../../api/errors'
import {
  getListChannelsQueryKey,
  useCheckChannel,
  useCompleteWooCommerceAuthorization,
  useConnectWooCommerce,
  useListChannels,
  useStartWooCommerceAuthorization,
} from '../../api/generated/channels/channels'
import type { SalesChannelDto } from '../../api/generated/model'
import { Eyebrow } from '../../components/Eyebrow'
import { BoxLoader, PageLoader } from '../../components/loaders'
import { formatDateTime } from '../../lib/format'
import { leaveTo } from '../../lib/navigation'
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
  // The shop sends the user back here after its approval page, adding success=1 or success=0.
  const [params] = useSearchParams()
  const authorizationId = params.get('authorization')

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
        authorizationId ? (
          <AuthorizationReturn id={authorizationId} approved={params.get('success') === '1'} />
        ) : (
          <Connect />
        )
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

/** Connecting a shop: through its approval page, or with keys entered by hand when that is blocked. */
function Connect() {
  const [manual, setManual] = useState(false)
  // Kept here, so switching between the two ways keeps the typed address.
  const [baseUrl, setBaseUrl] = useState('')

  return (
    <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="lg" style={{ alignItems: 'start' }}>
      {manual ? (
        <KeysForm baseUrl={baseUrl} onBaseUrlChange={setBaseUrl} onBack={() => setManual(false)} />
      ) : (
        <ApprovalForm baseUrl={baseUrl} onBaseUrlChange={setBaseUrl} onManual={() => setManual(true)} />
      )}
      {manual ? <KeysHelp /> : <ApprovalHelp />}
    </SimpleGrid>
  )
}

type BaseUrlProps = { baseUrl: string; onBaseUrlChange: (value: string) => void }

function BaseUrlInput({ baseUrl, onBaseUrlChange, error }: BaseUrlProps & { error?: string }) {
  const { t } = useTranslation()
  return (
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
      onChange={(e) => onBaseUrlChange(e.currentTarget.value)}
      error={error}
    />
  )
}

/** Sends the user to the shop, where its owner approves access and the shop sends the keys itself. */
function ApprovalForm({ baseUrl, onBaseUrlChange, onManual }: BaseUrlProps & { onManual: () => void }) {
  const { t } = useTranslation()
  const start = useStartWooCommerceAuthorization({
    mutation: { onSuccess: (authorization) => leaveTo(authorization.authorizeUrl) },
  })

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    start.mutate({ data: { baseUrl } })
  }

  const errors = fieldErrorMessages(t, start.error)
  const formError = formErrorMessage(t, start.error)

  return (
    <Section title={t('channels.connect.title')}>
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          <Text fz="sm" c="dimmed">
            {t('channels.connect.intro')}
          </Text>
          <BaseUrlInput baseUrl={baseUrl} onBaseUrlChange={onBaseUrlChange} error={errors.baseUrl} />
          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}
          <Group justify="space-between" className="z-phone-stack">
            <Button variant="subtle" onClick={onManual}>
              {t('channels.connect.manual')}
            </Button>
            {/* Stays busy after success, while the browser is on its way to the shop. */}
            <Button type="submit" loading={start.isPending || start.isSuccess}>
              {t('channels.connect.submit')}
            </Button>
          </Group>
        </Stack>
      </form>
    </Section>
  )
}

function KeysForm({ baseUrl, onBaseUrlChange, onBack }: BaseUrlProps & { onBack: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
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
    <Section title={t('channels.connect.manualTitle')}>
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          <Text fz="sm" c="dimmed">
            {t('channels.connect.manualIntro')}
          </Text>
          <BaseUrlInput baseUrl={baseUrl} onBaseUrlChange={onBaseUrlChange} error={errors.baseUrl} />
          <KeyFields keys={keys} onChange={setKeys} errors={errors} />
          {formError && (
            <Text role="alert" c="var(--mantine-color-error)" fz="sm">
              {formError}
            </Text>
          )}
          <Group justify="space-between" className="z-phone-stack">
            <Button variant="subtle" onClick={onBack}>
              {t('channels.connect.back')}
            </Button>
            <Button type="submit" loading={connect.isPending}>
              {t('channels.connect.manualSubmit')}
            </Button>
          </Group>
        </Stack>
      </form>
    </Section>
  )
}

/**
 * Back from the shop's approval page: saves the shop with the keys it sent. Runs once on arrival;
 * a second call would race the first for the same shop.
 */
function AuthorizationReturn({ id, approved }: { id: string; approved: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const complete = useCompleteWooCommerceAuthorization({
    mutation: {
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: getListChannelsQueryKey() })
        navigate('/channels', { replace: true })
      },
    },
  })
  const started = useRef(false)
  const { mutate } = complete

  useEffect(() => {
    if (!approved || started.current) return
    started.current = true
    mutate({ id })
  }, [approved, id, mutate])

  const restart = () => navigate('/channels', { replace: true })
  const error = formErrorMessage(t, complete.error)

  return (
    <Box maw={640}>
      <Section title={t('channels.authorization.title')}>
        {approved && !complete.isError ? (
          <BoxLoader label={t('channels.authorization.connecting')} placement="inline" />
        ) : (
          <Stack gap="md">
            <Text role="alert" fz="sm" c={approved ? 'var(--mantine-color-error)' : 'var(--z-text-2)'}>
              {approved ? error : t('channels.authorization.denied')}
            </Text>
            <Group gap="sm" className="z-phone-stack">
              {approved && <Button onClick={() => complete.mutate({ id })}>{t('channels.authorization.retry')}</Button>}
              <Button variant={approved ? 'default' : undefined} onClick={restart}>
                {t('channels.authorization.restart')}
              </Button>
            </Group>
          </Stack>
        )}
      </Section>
    </Box>
  )
}

function ApprovalHelp() {
  const { t } = useTranslation()
  return (
    <Section title={t('channels.how.title')}>
      <Stack gap="md">
        <List type="ordered" spacing="sm" fz="sm">
          <List.Item>{t('channels.how.step1')}</List.Item>
          <List.Item>{t('channels.how.step2')}</List.Item>
          <List.Item>{t('channels.how.step3')}</List.Item>
        </List>
        <Text fz="sm" c="dimmed">
          {t('channels.how.blocked')}
        </Text>
        <Text fz="sm" c="dimmed">
          {t('channels.privacy')}
        </Text>
      </Stack>
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
