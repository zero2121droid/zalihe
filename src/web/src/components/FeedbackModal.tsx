import { Box, Button, Checkbox, Group, Modal, Stack, Text, Textarea, Title } from '@mantine/core'
import { IconBug, IconBulb, IconCut } from '@tabler/icons-react'
import { type ClipboardEvent, type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router'
import { fieldErrorMessages, formErrorMessage } from '../api/errors'
import { useSendFeedback } from '../api/generated/feedback/feedback'
import { FeedbackKind } from '../api/generated/model'
import { useCurrentUser } from '../auth/useCurrentUser'
import { describeClient } from '../lib/client'
import { useScreenshot } from '../lib/useScreenshot'
import { useShake } from '../lib/useShake'
import { Eyebrow } from './Eyebrow'
import { FilterPill, FilterPills } from './FilterPill'
import classes from './FeedbackModal.module.css'
import { ScreenshotField } from './ScreenshotField'

type WrittenKind = Exclude<FeedbackKind, 'prompt'>

const kinds: { kind: WrittenKind; icon: typeof IconBulb }[] = [
  { kind: FeedbackKind.suggestion, icon: IconBulb },
  { kind: FeedbackKind.problem, icon: IconBug },
  { kind: FeedbackKind.confusing, icon: IconCut },
]

/**
 * A message to the platform's owner from any screen (SPEC 11.10): what is missing, what doesn't work,
 * what is unneeded. The page and browser go with it, and the user sees exactly that before sending.
 */
export function FeedbackModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const { user } = useCurrentUser()
  const { pathname } = useLocation()
  const [kind, setKind] = useState<WrittenKind>(FeedbackKind.suggestion)
  const [message, setMessage] = useState('')
  const [mayContact, setMayContact] = useState(true)
  const [missing, setMissing] = useState(false)
  const [sent, setSent] = useState(false)
  const screenshot = useScreenshot()
  const { className: shakeClass, shake } = useShake()
  const send = useSendFeedback({ mutation: { onSuccess: () => setSent(true) } })

  function reset() {
    setKind(FeedbackKind.suggestion)
    setMessage('')
    setMissing(false)
    screenshot.clear()
    setSent(false)
    send.reset()
  }

  function close() {
    onClose()
    reset()
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!message.trim()) {
      setMissing(true)
      shake()
      return
    }
    send.mutate({ data: { kind, message: message.trim(), page: pathname, mayContact, ...(screenshot.value && { screenshot: screenshot.value }) } })
  }

  // A picture pasted anywhere in the form becomes the screenshot; pasted text goes where it was pasted.
  function handlePaste(event: ClipboardEvent<HTMLFormElement>) {
    const image = Array.from(event.clipboardData.files).find((file) => file.type.startsWith('image/'))
    if (!image) return
    event.preventDefault()
    void screenshot.pick(image)
  }

  const fieldErrors = fieldErrorMessages(t, send.error)
  const messageError = missing ? t('errors.validation.required') : fieldErrors.message
  const formError = formErrorMessage(t, send.error)
  const context = [pathname, describeClient(navigator.userAgent), t(`common.languages.${user?.language ?? 'sr-Latn'}`)]
    .filter(Boolean)
    .join(' · ')

  return (
    <Modal opened={opened} onClose={close} title={sent ? undefined : t('feedback.title')}>
      {sent ? (
        <Stack align="center" ta="center" gap="sm" py="lg">
          <span className={classes.ring} aria-hidden>
            <svg viewBox="0 0 24 24">
              <polyline points="5,12.5 9.5,17 19,7.5" />
            </svg>
          </span>
          <Title order={2} fz={18} role="status">
            {t('feedback.sent.title')}
          </Title>
          <Text fz="sm" c="var(--z-text-2)" maw={380}>
            {t('feedback.sent.text')}
          </Text>
          <Group gap="sm" mt="sm">
            <Button variant="default" onClick={reset}>
              {t('feedback.sent.another')}
            </Button>
            <Button onClick={close}>{t('feedback.sent.close')}</Button>
          </Group>
        </Stack>
      ) : (
        <form onSubmit={handleSubmit} onPaste={handlePaste} noValidate className={shakeClass}>
          <Stack gap="md">
            <Text fz="sm" c="dimmed">
              {t('feedback.intro')}
            </Text>
            <Stack gap={6}>
              <Text fz="sm" fw={500}>
                {t('feedback.kindLabel')}
              </Text>
              <FilterPills label={t('feedback.kindLabel')}>
                {kinds.map(({ kind: value, icon: Icon }) => (
                  <FilterPill key={value} active={kind === value} onClick={() => setKind(value)}>
                    <Icon size={15} stroke={1.8} className={classes.kindIcon} data-active={kind === value || undefined} />
                    {t(`feedback.kinds.${value}`)}
                  </FilterPill>
                ))}
              </FilterPills>
            </Stack>
            <Textarea
              label={t('feedback.message')}
              placeholder={t(`feedback.placeholders.${kind}`)}
              rows={5}
              resize="vertical"
              maxLength={4000}
              data-autofocus
              value={message}
              error={messageError}
              onChange={(e) => {
                setMessage(e.currentTarget.value)
                setMissing(false)
              }}
            />
            <ScreenshotField screenshot={screenshot} error={fieldErrors.screenshot} />
            <Checkbox
              checked={mayContact}
              onChange={(e) => setMayContact(e.currentTarget.checked)}
              label={t('feedback.mayContact', { email: user?.email ?? '' })}
            />
            <Box className={classes.context}>
              <Eyebrow>{t('feedback.context')}</Eyebrow>
              <Text ff="monospace" fz="xs" c="var(--z-text-2)" style={{ overflowWrap: 'anywhere' }}>
                {context}
              </Text>
            </Box>
            {formError && (
              <Text role="alert" c="var(--mantine-color-error)" fz="sm">
                {formError}
              </Text>
            )}
            <Group justify="flex-end" gap="sm" className="z-modal-actions">
              <Button variant="default" className="z-modal-cancel" onClick={close}>
                {t('common.cancel')}
              </Button>
              <Button type="submit" loading={send.isPending} disabled={screenshot.shrinking}>
                {t('feedback.send')}
              </Button>
            </Group>
          </Stack>
        </form>
      )}
    </Modal>
  )
}
