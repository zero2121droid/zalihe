import { Button, Group, Stack, Text, Textarea } from '@mantine/core'
import { IconCheck } from '@tabler/icons-react'
import { useQueryClient } from '@tanstack/react-query'
import { type SubmitEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router'
import { formErrorMessage } from '../api/errors'
import {
  getListAnsweredFeedbackPromptsQueryKey,
  useAnswerFeedbackPrompt,
  useListAnsweredFeedbackPrompts,
} from '../api/generated/feedback/feedback'
import type { FeedbackPrompt } from '../api/generated/model'
import { FilterPill } from './FilterPill'

type Phase = 'ask' | 'why' | 'thanks'

/**
 * A question asked once per user, right after the moment it is about (SPEC 11.10), e.g. "Was the import
 * clear?". "Yes" is only counted; "Not really" opens a field for one sentence, which can be skipped.
 * Shown inline, never over the screen, and not at all once the user has answered.
 */
export function FeedbackQuestion({ prompt, question }: { prompt: FeedbackPrompt; question: string }) {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const queryClient = useQueryClient()
  const answered = useListAnsweredFeedbackPrompts()
  const [phase, setPhase] = useState<Phase>('ask')
  const [message, setMessage] = useState('')
  const answer = useAnswerFeedbackPrompt({
    mutation: {
      onSuccess: () => {
        setPhase('thanks')
        void queryClient.invalidateQueries({ queryKey: getListAnsweredFeedbackPromptsQueryKey() })
      },
    },
  })

  // After answering, the thanks stays until the user leaves, even though the prompt is now answered.
  if (phase === 'ask' && (!answered.data || answered.data.includes(prompt))) return null

  function send(positive: boolean, text?: string) {
    answer.mutate({ prompt, data: { positive, message: text?.trim() || null, page: pathname } })
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    send(false, message)
  }

  const error = formErrorMessage(t, answer.error)

  return (
    <Stack gap="sm" pt="md" w="100%" style={{ borderTop: '1px solid var(--z-line-soft)' }}>
      {phase === 'thanks' ? (
        <Group gap={8} role="status">
          <IconCheck size={16} stroke={1.8} color="var(--z-accent-text)" />
          <Text fz="sm" c="var(--z-text-2)">
            {t('feedback.question.thanks')}
          </Text>
        </Group>
      ) : phase === 'ask' ? (
        <Group gap="sm" role="group" aria-label={question}>
          <Text fz="sm" c="var(--z-text-2)">
            {question}
          </Text>
          <Group gap={6}>
            <FilterPill active={false} onClick={() => send(true)}>
              {t('feedback.question.yes')}
            </FilterPill>
            <FilterPill active={false} onClick={() => setPhase('why')}>
              {t('feedback.question.no')}
            </FilterPill>
          </Group>
        </Group>
      ) : (
        <form onSubmit={handleSubmit} noValidate>
          <Stack gap="sm" maw={560}>
            <Textarea
              label={t('feedback.question.why')}
              rows={3}
              resize="vertical"
              maxLength={4000}
              autoFocus
              value={message}
              onChange={(e) => setMessage(e.currentTarget.value)}
            />
            <Group gap="sm" justify="flex-end">
              <Button variant="subtle" size="compact-md" onClick={() => send(false)} disabled={answer.isPending}>
                {t('feedback.question.skip')}
              </Button>
              <Button type="submit" size="compact-md" loading={answer.isPending}>
                {t('feedback.send')}
              </Button>
            </Group>
          </Stack>
        </form>
      )}
      {error && (
        <Text role="alert" c="var(--mantine-color-error)" fz="sm">
          {error}
        </Text>
      )}
    </Stack>
  )
}
