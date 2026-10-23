import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { renderRoutes } from '../test/render'
import { navItems, PHONE_TABS } from './navItems'
import { PhoneTabs } from './PhoneTabs'

const tabs = <PhoneTabs tabs={navItems.slice(0, PHONE_TABS)} more={navItems.slice(PHONE_TABS)} account={<p>Zrno</p>} />

describe('PhoneTabs', () => {
  it('Render_ItemsPage_MarksItemsTab', async () => {
    // Arrange & Act
    await renderRoutes([{ path: '/items/:id', element: tabs }], '/items/1')

    // Assert
    const bar = screen.getByRole('navigation', { name: 'Navigacija' })
    expect(within(bar).getByRole('link', { name: 'Artikli' })).toHaveAttribute('aria-current', 'page')
    expect(within(bar).getByRole('link', { name: 'Početna' })).not.toHaveAttribute('aria-current')
  })

  it('More_Clicked_ShowsOtherScreensAndAccount', async () => {
    // Arrange
    await renderRoutes([{ path: '/', element: tabs }], '/')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Više' }))

    // Assert
    const more = await screen.findByRole('dialog', { name: 'Više' })
    expect(within(more).getByRole('link', { name: 'Izveštaji' })).toBeInTheDocument()
    expect(within(more).getByRole('link', { name: 'Podešavanja' })).toBeInTheDocument()
    expect(within(more).getByText('Zrno')).toBeInTheDocument()
  })
})
