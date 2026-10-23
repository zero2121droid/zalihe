import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { withReducedMotion } from '../test/motion'
import { mockApi, renderPage } from '../test/render'
import { HomePage } from './HomePage'

const dashboard = {
  stockValue: 1284350,
  activeItems: 312,
  belowMinimum: 7,
  outOfStock: 3,
  reorder: [
    { itemId: 'a', name: 'Džezva bakarna 300 ml', sku: 'DZ-BAK-300', unit: 'kom', stock: 0, minStock: 3, status: 'outOfStock' },
    { itemId: 'b', name: 'Filter papir V60', sku: 'FP-V60-100', unit: 'pak', stock: 6, minStock: 15, status: 'low' },
  ],
  recentMovements: [
    { id: 'm1', itemId: 'c', itemName: 'Kafa Etiopija 250 g', type: 'sale', quantity: -2, occurredAt: new Date().toISOString(), source: 'wooCommerce', externalRef: '1048', note: null },
    { id: 'm2', itemId: 'd', itemName: 'Kafa Kolumbija 250 g', type: 'receipt', quantity: 24, occurredAt: '2020-01-05T10:00:00Z', source: 'manual', externalRef: null, note: 'Dobavljač' },
  ],
  ordersWithUnlinked: 0,
  setup: { hasItems: true, hasMinStock: true, hasShop: true },
}

describe('HomePage', () => {
  it('Render_CompanyWithData_ShowsFiguresReordersAndMovements', async () => {
    // Arrange
    withReducedMotion()
    mockApi({ 'GET /api/dashboard': { status: 200, body: dashboard } })

    // Act
    await renderPage(<HomePage />)

    // Assert
    expect(await screen.findByText('1.284.350')).toBeInTheDocument()
    expect(await screen.findByText('312')).toBeInTheDocument()
    expect(screen.getByText('Džezva bakarna 300 ml')).toBeInTheDocument()
    expect(screen.getByText('min. 15 pak')).toBeInTheDocument()
    expect(screen.getByText('Prodaja · WooCommerce #1048')).toBeInTheDocument()
    expect(screen.getByText('Prijem · Dobavljač')).toBeInTheDocument()
    expect(screen.getByText('−2')).toBeInTheDocument()
    expect(screen.getByText('+24')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Svi artikli ispod minimuma' })).toHaveAttribute('href', '/items?status=low')
  })

  it('Render_NewCompany_ShowsFirstStepsWithShopAsOptional', async () => {
    // Arrange
    mockApi({
      'GET /api/dashboard': {
        status: 200,
        body: {
          stockValue: 0,
          activeItems: 0,
          belowMinimum: 0,
          outOfStock: 0,
          reorder: [],
          recentMovements: [],
          ordersWithUnlinked: 0,
          setup: { hasItems: false, hasMinStock: false, hasShop: false },
        },
      },
    })

    // Act
    await renderPage(<HomePage />)

    // Assert
    const steps = await screen.findByRole('region', { name: 'Prvi koraci' })
    expect(within(steps).getByText('0 od 2')).toBeInTheDocument()
    expect(within(steps).getByRole('link', { name: 'Novi artikal' })).toHaveAttribute('href', '/items?new=1')
    expect(within(steps).getByRole('link', { name: 'Uvezi CSV' })).toHaveAttribute('href', '/items/import')
    expect(within(steps).getByText('nije obavezno')).toBeInTheDocument()
    expect(within(steps).getByRole('link', { name: 'Poveži prodavnicu' })).toHaveAttribute('href', '/channels')
    expect(screen.queryByText('Vrednost zaliha')).not.toBeInTheDocument()
  })

  it('Render_ItemsWithoutMinimum_ChecksOffItemsAndShowsOverview', async () => {
    // Arrange
    mockApi({
      'GET /api/dashboard': {
        status: 200,
        body: {
          stockValue: 0,
          activeItems: 3,
          belowMinimum: 0,
          outOfStock: 0,
          reorder: [],
          recentMovements: [],
          ordersWithUnlinked: 0,
          setup: { hasItems: true, hasMinStock: false, hasShop: false },
        },
      },
    })

    // Act
    await renderPage(<HomePage />)

    // Assert
    const steps = await screen.findByRole('region', { name: 'Prvi koraci' })
    expect(within(steps).getByText('1 od 2')).toBeInTheDocument()
    expect(within(steps).getByText('Artikli su dodati.')).toBeInTheDocument()
    expect(within(steps).queryByRole('link', { name: 'Novi artikal' })).not.toBeInTheDocument()
    expect(within(steps).getByRole('link', { name: 'Otvori artikle' })).toHaveAttribute('href', '/items')
  })

  it('Render_OrdersWithUnlinkedProducts_WarnsAndLinksToThem', async () => {
    // Arrange
    mockApi({ 'GET /api/dashboard': { status: 200, body: { ...dashboard, ordersWithUnlinked: 3 } } })

    // Act
    await renderPage(<HomePage />)

    // Assert
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('3 porudžbine imaju nepovezane proizvode')
    expect(within(alert).getByRole('link', { name: 'Poveži proizvode' })).toHaveAttribute('href', '/orders?unlinked=1')
  })

  it('Render_NoUnlinkedOrders_ShowsNoWarning', async () => {
    // Arrange
    mockApi({ 'GET /api/dashboard': { status: 200, body: dashboard } })

    // Act
    await renderPage(<HomePage />)

    // Assert
    await screen.findByText('Treba naručiti')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
