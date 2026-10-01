import { screen, waitFor } from '@testing-library/react'
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

const movementEntry = (movement: object) => ({
  id: crypto.randomUUID(),
  occurredAt: '2026-10-01T09:20:00Z',
  userName: 'Miljan',
  change: null,
  movement: { id: crypto.randomUUID(), type: 'receipt', quantity: 24, occurredAt: '2026-10-01T09:20:00Z', source: 'manual', externalRef: null, note: null, userName: 'Miljan', ...movement },
})

const changeEntry = (kind: string, changes: object[] = [], userName: string | null = 'Miljan') => ({
  id: crypto.randomUUID(),
  occurredAt: '2026-10-01T08:00:00Z',
  userName,
  movement: null,
  change: { kind, changes },
})

const historyPage = (items: object[]) => ({ status: 200, body: { items, totalCount: items.length, page: 1, pageSize: 20 } })

describe('ItemDetailPage', () => {
  it('Render_ItemWithHistory_ShowsStockMovementsAndChanges', async () => {
    // Arrange
    mockApi({
      'GET /api/items/item-1/history': historyPage([
        movementEntry({ type: 'sale', quantity: -2, source: 'wooCommerce', externalRef: '1048' }),
        changeEntry('updated', [
          { field: 'minStock', oldValue: '10', newValue: '12.5' },
          { field: 'purchasePrice', oldValue: '900', newValue: null },
          { field: 'unit', oldValue: 'kom', newValue: 'kg' },
        ]),
        changeEntry('created', [], null),
      ]),
      'GET /api/items/item-1': { status: 200, body: item },
    })

    // Act
    await renderRoutes(routes, '/items/item-1')

    // Assert
    expect(await screen.findByRole('heading', { name: 'Kafa Etiopija 250 g' })).toBeInTheDocument()
    expect(await screen.findByText('−2')).toBeInTheDocument()
    expect(screen.getByText('WooCommerce #1048')).toBeInTheDocument()
    expect(screen.getByText('Artikal izmenjen')).toBeInTheDocument()
    expect(screen.getByText(/10 kom → 12,5 kom/)).toBeInTheDocument()
    expect(screen.getByText(/900 RSD → prazno/)).toBeInTheDocument()
    expect(screen.getByText(/kom → kg/)).toBeInTheDocument()
    expect(screen.getByText('Artikal napravljen')).toBeInTheDocument()
  })

  it('HistoryFilter_ChangesClicked_RequestsOnlyItemChanges', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/items/item-1/history': historyPage([changeEntry('created')]),
      'GET /api/items/item-1': { status: 200, body: item },
    })
    await renderRoutes(routes, '/items/item-1')
    await screen.findByText('Artikal napravljen')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Izmene artikla' }))

    // Assert
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/items/item-1/history?filter=changes&page=1&pageSize=20', expect.anything()),
    )
    expect(screen.getByRole('button', { name: 'Izmene artikla' })).toHaveAttribute('aria-pressed', 'true')
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
      'GET /api/items/item-1/history': historyPage([]),
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
