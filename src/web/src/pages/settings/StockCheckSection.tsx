import { Box, Button, Group, Stack, Text, Title } from '@mantine/core'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { formErrorMessage } from '../../api/errors'
import { useRecalculateStock } from '../../api/generated/stock/stock'
import { formatNumber } from '../../lib/format'

const sectionStyle = { borderRadius: 'var(--mantine-radius-md)' }

/**
 * The administrative stock recalculation (SPEC 4). Normally everything matches; a correction lists
 * what changed, so the owner sees it and can tell us (it means a bug).
 */
export function StockCheckSection() {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  // Every screen that shows stock may have shown a wrong number.
  const recalculate = useRecalculateStock({ mutation: { onSuccess: () => queryClient.invalidateQueries() } })
  const result = recalculate.data
  const title = t('settings.stockCheck.title')
  const number = (value: number) => formatNumber(value, i18n.language)

  return (
    <Box component="section" aria-label={title} p="lg" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={sectionStyle}>
      <Stack gap="md" maw={640}>
        <Stack gap={2}>
          <Title order={2}>{title}</Title>
          <Text fz="sm" c="dimmed">
            {t('settings.stockCheck.description')}
          </Text>
        </Stack>
        <Group>
          <Button variant="default" loading={recalculate.isPending} onClick={() => recalculate.mutate()}>
            {t('settings.stockCheck.run')}
          </Button>
        </Group>
        {recalculate.isError && (
          <Text role="alert" fz="sm" c="var(--mantine-color-error)">
            {formErrorMessage(t, recalculate.error)}
          </Text>
        )}
        {result && result.corrected.length === 0 && (
          <Text role="status" fz="sm" c="var(--z-accent-text)">
            {t('settings.stockCheck.allGood', { count: result.checkedCount, formatted: number(result.checkedCount) })}
          </Text>
        )}
        {result && result.corrected.length > 0 && (
          <Stack gap={4} role="status">
            <Text fz="sm" c="var(--z-status-low)">
              {t('settings.stockCheck.corrected', { count: result.corrected.length })}
            </Text>
            {result.corrected.map((c) => (
              <Text key={c.itemId} fz="sm">
                {c.name}{' '}
                <Text component="span" ff="monospace" fz="xs" c="dimmed">
                  {c.sku}
                </Text>{' '}
                <Text component="span" ff="monospace" fz="sm">
                  {number(c.before)} → {number(c.after)} {t(`items.units.${c.unit}`)}
                </Text>
              </Text>
            ))}
          </Stack>
        )}
      </Stack>
    </Box>
  )
}
