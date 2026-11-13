import { Anchor, Box, Button, Group, SimpleGrid, Stack, Text } from '@mantine/core'
import { IconBug, IconBulb, IconCut, IconMessageQuestion } from '@tabler/icons-react'
import { keepPreviousData, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { formErrorMessage } from '../../api/errors'
import { FeedbackStatus, type PlatformFeedbackDto } from '../../api/generated/model'
import {
  getGetPlatformFeedbackSummaryQueryKey,
  getGetPlatformFeedbackScreenshotUrl,
  getListPlatformFeedbackQueryKey,
  useGetPlatformFeedbackSummary,
  useListPlatformFeedback,
  useSetPlatformFeedbackStatus,
} from '../../api/generated/platform/platform'
import { FilterPill, FilterPills } from '../../components/FilterPill'
import { EmptyBox, SkeletonRows } from '../../components/loaders'
import { PageTurn } from '../../components/PageTurn'
import { Pager } from '../../components/Pager'
import { describeClient } from '../../lib/client'
import { formatDateTime, formatNumber } from '../../lib/format'
import { DEFAULT_PAGE_SIZE, parsePageSize } from '../../lib/pageSize'
import { useLoadingDisplay } from '../../lib/useLoadingDisplay'
import classes from './FeedbackInbox.module.css'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

/** The filter in the URL: "new" when absent, since that is what needs the owner. */
type Filter = FeedbackStatus | 'all'
const filters: Filter[] = [FeedbackStatus.new, FeedbackStatus.read, FeedbackStatus.done, 'all']

const kindIcons = { suggestion: IconBulb, problem: IconBug, confusing: IconCut, prompt: IconMessageQuestion }

/** The users' messages from all companies (SPEC 11.10), for the platform's owner. */
export function FeedbackInbox() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()
  const filter = (filters as string[]).includes(params.get('status') ?? '') ? (params.get('status') as Filter) : FeedbackStatus.new
  const page = Math.max(1, Number(params.get('page')) || 1)
  const pageSize = parsePageSize(params.get('size'))
  const update = (changes: { filter?: Filter; page?: number; pageSize?: number }) => {
    const next = { filter, page: 1, pageSize, ...changes }
    setParams({
      ...(next.filter !== FeedbackStatus.new && { status: next.filter }),
      ...(next.page > 1 && { page: String(next.page) }),
      ...(next.pageSize !== DEFAULT_PAGE_SIZE && { size: String(next.pageSize) }),
    })
  }

  const list = useListPlatformFeedback(
    { status: filter === 'all' ? undefined : filter, page, pageSize },
    { query: { placeholderData: keepPreviousData } },
  )
  const display = useLoadingDisplay(list.isPending)
  const data = list.data

  return (
    <Stack gap="lg">
      <Summary />

      <FilterPills label={t('platform.feedback.filter')}>
        {filters.map((value) => (
          <FilterPill key={value} active={filter === value} onClick={() => update({ filter: value })}>
            {t(`platform.feedback.filters.${value}`)}
          </FilterPill>
        ))}
      </FilterPills>

      <Box component="section" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
        {list.isPending || display !== 'content' ? (
          display === 'loader' && <SkeletonRows />
        ) : list.isError ? (
          <Text role="alert" p="lg">
            {formErrorMessage(t, list.error)}
          </Text>
        ) : data!.totalCount === 0 ? (
          <Stack gap="sm" p="lg" align="flex-start">
            <EmptyBox />
            <Text fz="sm" c="dimmed">
              {t(filter === FeedbackStatus.new ? 'platform.feedback.emptyNew' : 'platform.feedback.empty')}
            </Text>
          </Stack>
        ) : (
          <>
            <PageTurn page={data!.page}>
              <Box component="ul" className={classes.list}>
                {data!.items.map((feedback) => (
                  <FeedbackRow key={feedback.id} feedback={feedback} />
                ))}
              </Box>
            </PageTurn>
            <Pager
              page={page}
              pageSize={pageSize}
              totalCount={data!.totalCount}
              onChange={(next) => update({ page: next })}
              onPageSizeChange={(next) => update({ pageSize: next })}
            />
          </>
        )}
      </Box>
    </Stack>
  )
}

function Summary() {
  const { t, i18n } = useTranslation()
  const summary = useGetPlatformFeedbackSummary()
  if (summary.isError) return <Text role="alert">{formErrorMessage(t, summary.error)}</Text>

  const data = summary.data
  const number = (value: number | undefined) => (value === undefined ? '—' : formatNumber(value, i18n.language))
  return (
    <SimpleGrid
      component="section"
      aria-label={t('platform.feedback.summary')}
      className="z-kpi"
      cols={{ base: 2, md: 5 }}
      spacing={1}
      verticalSpacing={1}
      bg="var(--z-line)"
      bd="1px solid var(--z-line)"
      style={{ ...sectionStyle, overflow: 'hidden' }}
    >
      <Figure label={t('platform.feedback.figures.new')} value={number(data?.new)} attention={!!data?.new} />
      <Figure label={t('platform.feedback.figures.suggestions')} value={number(data?.suggestions)} />
      <Figure label={t('platform.feedback.figures.problems')} value={number(data?.problems)} />
      <Figure label={t('platform.feedback.figures.done')} value={number(data?.done)} />
      <Figure
        label={t('platform.feedback.figures.csvImport')}
        value={data ? `${number(data.csvImportClear)} / ${number(data.csvImportAnswers)}` : '—'}
        hint={t('platform.feedback.figures.csvImportHint')}
      />
    </SimpleGrid>
  )
}

function Figure({ label, value, hint, attention }: { label: string; value: string; hint?: string; attention?: boolean }) {
  return (
    <Stack gap={6} p="md" px="lg" bg="var(--z-surface)">
      <Text fz="xs" c="dimmed">
        {label}
      </Text>
      <Text
        ff="monospace"
        fz={26}
        fw={500}
        lts="-0.02em"
        c={attention ? 'var(--z-status-low)' : undefined}
        style={{ fontVariantNumeric: 'tabular-nums' }}
      >
        {value}
      </Text>
      {hint && (
        <Text fz="xs" c="dimmed">
          {hint}
        </Text>
      )}
    </Stack>
  )
}

function FeedbackRow({ feedback }: { feedback: PlatformFeedbackDto }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const setStatus = useSetPlatformFeedbackStatus({
    mutation: {
      onSuccess: () =>
        Promise.all([
          queryClient.invalidateQueries({ queryKey: getListPlatformFeedbackQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getGetPlatformFeedbackSummaryQueryKey() }),
        ]),
    },
  })
  const Icon = kindIcons[feedback.kind]
  const kind =
    feedback.kind === 'prompt' && feedback.prompt
      ? `${t(`platform.feedback.prompts.${feedback.prompt}`)} ${t(feedback.promptAnswer ? 'feedback.question.yes' : 'feedback.question.no')}`
      : t(`feedback.kinds.${feedback.kind}`)
  const change = (status: FeedbackStatus) => setStatus.mutate({ id: feedback.id, data: { status } })
  const client = describeClient(feedback.client)

  return (
    <li className={classes.row} data-new={feedback.status === FeedbackStatus.new || undefined}>
      <Group gap={6} wrap="nowrap" className={classes.kind} data-kind={feedback.kind}>
        <Icon size={15} stroke={1.8} />
        <Text fz="xs" c="var(--z-text-2)">
          {kind}
        </Text>
      </Group>

      <Stack gap={6} miw={0}>
        <Text fz="sm" fw={feedback.status === FeedbackStatus.new ? 500 : 400} className={classes.message}>
          {feedback.message || t('platform.feedback.noText')}
        </Text>
        {feedback.hasScreenshot && (
          <a
            href={getGetPlatformFeedbackScreenshotUrl(feedback.id)}
            target="_blank"
            rel="noreferrer"
            className={`z-focus ${classes.screenshot}`}
            aria-label={t('platform.feedback.openScreenshot')}
          >
            <img src={getGetPlatformFeedbackScreenshotUrl(feedback.id)} alt="" loading="lazy" />
          </a>
        )}
        <Group gap={4} className={classes.details} fz="xs" c="dimmed">
          <span>
            {feedback.company} · {feedback.userName}
          </span>
          {feedback.page && (
            <Text span ff="monospace" fz="xs" c="var(--z-text-2)">
              {feedback.page}
            </Text>
          )}
          {client && <span>{client}</span>}
          {feedback.contactEmail && (
            <Anchor href={`mailto:${feedback.contactEmail}`} fz="xs">
              {feedback.contactEmail}
            </Anchor>
          )}
        </Group>
      </Stack>

      <Stack gap={8} className={classes.side}>
        <Group gap={8} wrap="nowrap">
          <span className={classes.status} data-status={feedback.status}>
            {t(`platform.feedback.statuses.${feedback.status}`)}
          </span>
          <Text ff="monospace" fz="xs" c="dimmed" style={{ whiteSpace: 'nowrap' }}>
            {formatDateTime(feedback.createdAt, i18n.language)}
          </Text>
        </Group>
        <Group gap={4} wrap="nowrap">
          {feedback.status === FeedbackStatus.new && (
            <Button size="compact-xs" variant="subtle" onClick={() => change(FeedbackStatus.read)} loading={setStatus.isPending}>
              {t('platform.feedback.actions.read')}
            </Button>
          )}
          {feedback.status !== FeedbackStatus.done && (
            <Button size="compact-xs" variant="default" onClick={() => change(FeedbackStatus.done)} loading={setStatus.isPending}>
              {t('platform.feedback.actions.done')}
            </Button>
          )}
          {feedback.status !== FeedbackStatus.new && (
            <Button size="compact-xs" variant="subtle" onClick={() => change(FeedbackStatus.new)} loading={setStatus.isPending}>
              {t('platform.feedback.actions.reopen')}
            </Button>
          )}
        </Group>
      </Stack>
    </li>
  )
}
