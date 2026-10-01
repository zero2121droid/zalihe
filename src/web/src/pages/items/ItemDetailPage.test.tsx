import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../../test/render'
import { ItemDetailPage } from './ItemDetailPage'

const routes = [{ path: '/items/:id', element: <ItemDetailPage /> }]

const item = {
  id: 'item-1',
  name: 'Kafa Etiopija 250 g',
  sku: 'KF-ETI-250',
  barcode: null,
  unit: 'kom',
  category: 'Kafa · zrno',
  groupName: null,
  purchasePrice: 900,
  salePrice: null,
  minStock: 10,
  isActive: true,
  stock: 4,
  status: 'low',
  stockValue: 3600,
  sold30Days: 38,
}

const movement = (overrides: object) => ({
  id: crypto.randomUUID(),
  type: 'receipt',
  quantity: 24,
  occurredAt: '2026-10-01T09:20:00Z',
  source: 'manual',
  externalRef: null,
  note: null,
  userName: 'Miljan',
  ...overrides,
})

const emptyHistory = { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 20 } }

describe('ItemDetailPage', () => {
  it('Render_ItemWithHistory_ShowsStockAndMovements', async () => {
    // Arrange
    mockApi({
      'GET /api/items/item-1/movements': {
        status: 200,
        body: {
          items: [
            movement({ type: 'sale', quantity: -2, source: 'wooCommerce', externalRef: '1048' }),
            movement({ type: 'adjustment', quantity: -18, note: 'Popis' }),
          ],
          totalCount: 2,
          page: 1,
          pageSize: 20,
        },
      },
      'GET /api/items/item-1': { status: 200, body: item },
    })

    // Act
    await renderRoutes(routes, '/items/item-1')

    // Assert
    expect(await screen.findByRole('heading', { name: 'Kafa Etiopija 250 g' })).toBeInTheDocument()
    expect(screen.getByText('Ispod minimuma')).toBeInTheDocument()
    expect(await screen.findByText('−2')).toBeInTheDocument()
    expect(screen.getByText('WooCommerce #1048')).toBeInTheDocument()
    expect(screen.getByText('Popis')).toBeInTheDocument()
    expect(screen.getAllByText('Miljan')).toHaveLength(2)
  })

  it('Render_UnknownItem_ShowsNotFound', async () => {
    // Arrange
    mockApi({ 'GET /api/items/item-1': { status: 404, body: { status: 404, code: 'common.not_found' } } })

    // Act
    await renderRoutes(routes, '/items/item-1')

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Artikal ne postoji ili ne pripada tvojoj firmi.')
  })

  it('ReceiptButton_Clicked_OpensReceiptForm', async () => {
    // Arrange
    mockApi({
      'GET /api/items/item-1/movements': emptyHistory,
      'GET /api/items/item-1': { status: 200, body: item },
    })
    await renderRoutes(routes, '/items/item-1')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Prijem' }))

    // Assert
    expect(await screen.findByText('Prijem robe')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Primljena količina/)).toBeInTheDocument()
  })
})
