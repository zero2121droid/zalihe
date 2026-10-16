import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AppProviders } from '../AppProviders'
import i18n from '../i18n'
import { Pager } from './Pager'

function renderPager(page: number, onChange = vi.fn(), onPageSizeChange = vi.fn()) {
  render(
    <AppProviders>
      <Pager page={page} pageSize={20} totalCount={200} onChange={onChange} onPageSizeChange={onPageSizeChange} />
    </AppProviders>,
  )
  return { onChange, onPageSizeChange }
}

describe('Pager', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('sr-Latn')
  })

  it('Click_PageNumber_JumpsToThatPage', async () => {
    // Arrange
    const { onChange } = renderPager(1)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Strana 10' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith(10)
  })

  it('Render_MiddlePage_MarksCurrentPageAndShowsRange', () => {
    // Arrange & Act
    renderPager(5)

    // Assert
    expect(screen.getByRole('button', { name: 'Strana 5' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByText('81–100 od 200')).toBeInTheDocument()
  })

  it('Render_FirstPage_DisablesPreviousOnly', () => {
    // Arrange & Act
    renderPager(1)

    // Assert
    expect(screen.getByRole('button', { name: 'Prethodna strana' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Sledeća strana' })).toBeEnabled()
  })

  it('SelectPageSize_FiftyPicked_ReportsNewSize', async () => {
    // Arrange
    const { onPageSizeChange } = renderPager(1)

    // Act
    await userEvent.click(screen.getByLabelText('Po strani'))
    // jsdom never finishes the dropdown's open transition, so the options count as hidden.
    await userEvent.click(await screen.findByRole('option', { name: '50', hidden: true }))

    // Assert
    expect(onPageSizeChange).toHaveBeenCalledWith(50)
  })
})
