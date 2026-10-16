import { Group, Pagination, Select, Text } from '@mantine/core'
import { useId } from '@mantine/hooks'
import { IconChevronLeft, IconChevronRight } from '@tabler/icons-react'
import { useTranslation } from 'react-i18next'
import { formatNumber } from '../lib/format'
import { PAGE_SIZES } from '../lib/pageSize'
import classes from './Pager.module.css'

interface PagerProps {
  page: number
  pageSize: number
  totalCount: number
  onChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

/**
 * Table footer: the shown range and the page size picker on the left,
 * page numbers with previous/next arrows on the right.
 */
export function Pager({ page, pageSize, totalCount, onChange, onPageSizeChange }: PagerProps) {
  const { t, i18n } = useTranslation()
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const format = (value: number) => formatNumber(value, i18n.language)
  const sizeId = useId()

  return (
    <Group justify="space-between" px="lg" py="sm" gap="sm">
      <Group gap="md">
        <Text fz="sm" c="dimmed">
          {t('items.range', {
            from: format((page - 1) * pageSize + 1),
            to: format(Math.min(page * pageSize, totalCount)),
            total: format(totalCount),
          })}
        </Text>
        <Group gap={8}>
          <Text component="label" htmlFor={sizeId} fz="sm" c="dimmed">
            {t('common.pageSize')}
          </Text>
          <Select
            id={sizeId}
            w={80}
            classNames={{ input: classes.sizeInput }}
            data={PAGE_SIZES.map((size) => ({ value: String(size), label: format(size) }))}
            value={String(pageSize)}
            onChange={(value) => value && onPageSizeChange(Number(value))}
            allowDeselect={false}
            comboboxProps={{ withinPortal: true }}
          />
        </Group>
      </Group>
      <Pagination
        component="nav"
        aria-label={t('common.pagination')}
        total={totalPages}
        value={page}
        onChange={onChange}
        siblings={1}
        boundaries={1}
        gap={6}
        classNames={{ control: classes.control, dots: classes.dots }}
        previousIcon={() => <IconChevronLeft size={14} stroke={2} />}
        nextIcon={() => <IconChevronRight size={14} stroke={2} />}
        getItemProps={(value) => ({
          'aria-label': t('common.page', { page: format(value) }),
          'aria-current': value === page ? 'page' : undefined,
          children: format(value),
        })}
        getControlProps={(control) =>
          control === 'previous'
            ? { 'aria-label': t('common.previousPage') }
            : control === 'next'
              ? { 'aria-label': t('common.nextPage') }
              : {}
        }
      />
    </Group>
  )
}
