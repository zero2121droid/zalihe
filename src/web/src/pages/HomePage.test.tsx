import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
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
}

describe('HomePage', () => {
  it('Render_CompanyWithData_ShowsFiguresReordersAndMovements', async () => {
    // Arrange
    mockApi({ 'GET /api/dashboard': { status: 200, body: dashboard } })

    // Act
    await renderPage(<HomePage />)

    // Assert
    expect(await screen.findByText('1.284.350')).toBeInTheDocument()
    expect(screen.getByText('312')).toBeInTheDocument()
    expect(screen.getByText('Džezva bakarna 300 ml')).toBeInTheDocument()
    expect(screen.getByText('min. 15 pak')).toBeInTheDocument()
    expect(screen.getByText('Prodaja · WooCommerce #1048')).toBeInTheDocument()
    expect(screen.getByText('Prijem · Dobavljač')).toBeInTheDocument()
    expect(screen.getByText('−2')).toBeInTheDocument()
    expect(screen.getByText('+24')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Svi artikli ispod minimuma' })).toHaveAttribute('href', '/items?status=low')
  })

  it('Render_NewCompany_OffersToAddOrImportItems', async () => {
    // Arrange
    mockApi({
      'GET /api/dashboard': {
        status: 200,
        body: { stockValue: 0, activeItems: 0, belowMinimum: 0, outOfStock: 0, reorder: [], recentMovements: [] },
      },
    })

    // Act
    await renderPage(<HomePage />)

    // Assert
    expect(await screen.findByText('Još nema artikala')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Uvezi CSV' })).toHaveAttribute('href', '/items/import')
  })
})
