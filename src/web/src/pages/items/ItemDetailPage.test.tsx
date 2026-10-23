import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { onPhone } from '../../test/phone'
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
  imageUrl: null,
  hasOwnImage: false,
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

// The chart's request; without it the fake API would answer it with the item, as its URL starts the same.
const stockHistory = {
  'GET /api/items/item-1/stock-history': {
    status: 200,
    body: { from: '2026-09-23T00:00:00Z', to: '2026-10-23T00:00:00Z', startStock: 0, points: [{ at: '2026-10-01T09:20:00Z', stock: 4 }] },
  },
}

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
      ...stockHistory,
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

  it('Render_HistoryOnPhone_ShowsWhatHappenedAndTheQuantityInTwoColumns', async () => {
    // Arrange
    onPhone()
    mockApi({
      'GET /api/items/item-1/history': historyPage([
        movementEntry({ type: 'sale', quantity: -2, source: 'wooCommerce', externalRef: '1048', note: 'Popis' }),
        changeEntry('updated', [{ field: 'minStock', oldValue: '10', newValue: '12.5' }]),
      ]),
      ...stockHistory,
      'GET /api/items/item-1': { status: 200, body: item },
    })

    // Act
    await renderRoutes(routes, '/items/item-1')

    // Assert
    const sale = (await screen.findByText('−2')).closest('tr')!
    expect(within(sale).getAllByRole('cell')).toHaveLength(2)
    expect(within(sale).getByText('Prodaja')).toBeInTheDocument()
    expect(within(sale).getByText(/^WooCommerce #1048 · .+ · Miljan$/)).toBeInTheDocument()
    expect(within(sale).getByText('Popis')).toBeInTheDocument()
    const change = screen.getByText('Artikal izmenjen').closest('tr')!
    expect(within(change).getByText(/10 kom → 12,5 kom/)).toBeInTheDocument()
    expect(screen.queryByRole('columnheader')).not.toBeInTheDocument()
  })

  it('HistoryFilter_ChangesClicked_RequestsOnlyItemChanges', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/items/item-1/history': historyPage([changeEntry('created')]),
      ...stockHistory,
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

  it('Deactivate_FromEditForm_OffersUndoThatActivatesAgain', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/items/categories': { status: 200, body: [] },
      'GET /api/items/item-1/history': historyPage([]),
      ...stockHistory,
      'GET /api/items/item-1': { status: 200, body: item },
      'POST /api/items/item-1/deactivate': { status: 204 },
      'POST /api/items/item-1/activate': { status: 204 },
    })
    await renderRoutes(routes, '/items/item-1')
    await userEvent.click(await screen.findByRole('button', { name: 'Izmeni' }))

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Deaktiviraj' }))
    await screen.findByText('„Kafa Etiopija 250 g” je deaktiviran.')
    await userEvent.click(screen.getByRole('button', { name: 'Poništi' }))

    // Assert
    expect(fetchMock).toHaveBeenCalledWith('/api/items/item-1/activate', expect.objectContaining({ method: 'POST' }))
    expect(screen.queryByText('„Kafa Etiopija 250 g” je deaktiviran.')).not.toBeInTheDocument()
  })

  it('UndoToast_TimeRunsOut_GoesAwayWithoutActivating', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/items/categories': { status: 200, body: [] },
      'GET /api/items/item-1/history': historyPage([]),
      ...stockHistory,
      'GET /api/items/item-1': { status: 200, body: item },
      'POST /api/items/item-1/deactivate': { status: 204 },
    })
    await renderRoutes(routes, '/items/item-1')
    await userEvent.click(await screen.findByRole('button', { name: 'Izmeni' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Deaktiviraj' }))
    const timer = (await screen.findByText(/je deaktiviran/)).closest('[role="status"]')!.lastElementChild!

    // Act: the five-second line finishes. jsdom has no AnimationEvent, so React listens for the
    // prefixed name there.
    fireEvent(timer, new Event('webkitAnimationEnd', { bubbles: true }))

    // Assert
    expect(screen.queryByText(/je deaktiviran/)).not.toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalledWith('/api/items/item-1/activate', expect.anything())
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
      ...stockHistory,
      'GET /api/items/item-1': { status: 200, body: item },
    })
    await renderRoutes(routes, '/items/item-1')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Prijem' }))

    // Assert
    expect(await screen.findByText('Prijem robe')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Primljena količina/)).toBeInTheDocument()
  })

  it('Render_StockNotInShopYet_SaysItIsBeingSent', async () => {
    // Arrange
    mockApi({
      'GET /api/items/item-1/history': historyPage([]),
      'GET /api/items/item-1/shop-stock': { status: 200, body: { shops: 1, pending: true } },
      ...stockHistory,
      'GET /api/items/item-1': { status: 200, body: item },
    })

    // Act
    await renderRoutes(routes, '/items/item-1')

    // Assert
    expect(await screen.findByText('Šalje se u prodavnicu')).toBeInTheDocument()
  })

  it('Render_StockInShop_SaysShopShowsIt', async () => {
    // Arrange
    mockApi({
      'GET /api/items/item-1/history': historyPage([]),
      'GET /api/items/item-1/shop-stock': { status: 200, body: { shops: 2, pending: false } },
      ...stockHistory,
      'GET /api/items/item-1': { status: 200, body: item },
    })

    // Act
    await renderRoutes(routes, '/items/item-1')

    // Assert
    expect(await screen.findByText('Isto stanje je u prodavnicama')).toBeInTheDocument()
  })

  it('Render_ItemInNoShop_SaysNothingAboutShops', async () => {
    // Arrange
    mockApi({
      'GET /api/items/item-1/history': historyPage([]),
      'GET /api/items/item-1/shop-stock': { status: 200, body: { shops: 0, pending: false } },
      ...stockHistory,
      'GET /api/items/item-1': { status: 200, body: item },
    })

    // Act
    await renderRoutes(routes, '/items/item-1')

    // Assert
    await screen.findByRole('heading', { name: 'Kafa Etiopija 250 g' })
    expect(screen.queryByText(/prodavnic/)).not.toBeInTheDocument()
  })

  it('ChartPeriod_NinetyDaysClicked_AsksForNinetyDays', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/items/item-1/history': historyPage([]),
      ...stockHistory,
      'GET /api/items/item-1': { status: 200, body: item },
    })
    await renderRoutes(routes, '/items/item-1')
    const chart = await screen.findByRole('region', { name: 'Stanje kroz vreme' })

    // Act
    await userEvent.click(within(chart).getByRole('button', { name: '90 dana' }))

    // Assert
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url) === '/api/items/item-1/stock-history?days=90')).toBe(true))
  })
})
