import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../../test/render'
import { ItemsPage } from './ItemsPage'

const routes = [
  { path: '/items', element: <ItemsPage /> },
  { path: '/items/:id', element: <div data-testid="detail" /> },
]

const item = (overrides: object = {}) => ({
  id: crypto.randomUUID(),
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
  ...overrides,
})

const page = (items: object[], totalCount = items.length) => ({ status: 200, body: { items, totalCount, page: 1, pageSize: 20 } })
const summary = { status: 200, body: { belowMinimum: 7, outOfStock: 3 } }

describe('ItemsPage', () => {
  it('Render_ItemsExist_ShowsStockColumnsAndRange', async () => {
    // Arrange
    mockApi({
      'GET /api/stock/summary': summary,
      'GET /api/items?': page([item(), item({ name: 'Espresso', sku: 'KF-ESP-1000', unit: 'kg', stock: 12.5, status: 'inStock', stockValue: null, sold30Days: 5 })], 42),
    })

    // Act
    await renderRoutes(routes, '/items')

    // Assert
    expect(await screen.findByText('Kafa Etiopija 250 g')).toBeInTheDocument()
    expect(screen.getByText('12,5')).toBeInTheDocument()
    expect(screen.getAllByText('Ispod minimuma').length).toBeGreaterThan(0)
    expect(screen.getByText('Na stanju')).toBeInTheDocument()
    expect(screen.getByText('3.600')).toBeInTheDocument()
    expect(screen.getByText('38')).toBeInTheDocument()
    expect(screen.getByText('1–20 od 42')).toBeInTheDocument()
  })

  it('Render_Summary_ShowsCountsOnFilterPills', async () => {
    // Arrange
    mockApi({ 'GET /api/stock/summary': summary, 'GET /api/items?': page([item()]) })

    // Act
    await renderRoutes(routes, '/items')

    // Assert
    expect(await screen.findByRole('button', { name: 'Ispod minimuma 7' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Nema na stanju 3' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Svi' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('StatusPill_Clicked_RequestsItemsInThatStatus', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/stock/summary': summary, 'GET /api/items?': page([item()]) })
    const { router } = await renderRoutes(routes, '/items')
    await screen.findByText('Kafa Etiopija 250 g')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Nema na stanju 3' }))

    // Assert
    await waitFor(() => expect(router.state.location.search).toBe('?status=outOfStock'))
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/items?status=outOfStock&page=1&pageSize=20', expect.anything()),
    )
  })

  it('Render_NoItems_ShowsEmptyState', async () => {
    // Arrange
    mockApi({ 'GET /api/stock/summary': summary, 'GET /api/items?': page([]) })

    // Act
    await renderRoutes(routes, '/items')

    // Assert
    expect(await screen.findByText('Još nema artikala')).toBeInTheDocument()
  })

  it('Search_Typed_RequestsFilteredListFromServer', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/stock/summary': summary, 'GET /api/items?': page([]) })
    const { router } = await renderRoutes(routes, '/items')
    await screen.findByText('Još nema artikala')

    // Act
    await userEvent.type(screen.getByRole('searchbox', { name: 'Pretraga artikala' }), 'etiop')

    // Assert
    await waitFor(() => expect(router.state.location.search).toBe('?q=etiop'))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/items?search=etiop&page=1&pageSize=20', expect.anything()))
    expect(await screen.findByText('Nema artikala za „etiop”.')).toBeInTheDocument()
  })

  it('Render_InactiveItem_ShowsInactiveLabel', async () => {
    // Arrange
    mockApi({ 'GET /api/stock/summary': summary, 'GET /api/items?': page([item({ isActive: false })]) })

    // Act
    await renderRoutes(routes, '/items?inactive=1')

    // Assert
    expect(await screen.findByText('Neaktivan')).toBeInTheDocument()
  })

  it('ShowInactive_Toggled_RequestsInactiveItems', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/stock/summary': summary, 'GET /api/items?': page([item()]) })
    const { router } = await renderRoutes(routes, '/items')
    await screen.findByText('Kafa Etiopija 250 g')

    // Act
    await userEvent.click(screen.getByRole('switch', { name: 'Prikaži neaktivne' }))

    // Assert
    await waitFor(() => expect(router.state.location.search).toBe('?inactive=1'))
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/items?includeInactive=true&page=1&pageSize=20', expect.anything()),
    )
  })

  it('ItemName_Clicked_OpensItemPage', async () => {
    // Arrange
    const kafa = item()
    mockApi({ 'GET /api/stock/summary': summary, 'GET /api/items?': page([kafa]) })
    const { router } = await renderRoutes(routes, '/items')

    // Act
    await userEvent.click(await screen.findByRole('link', { name: 'Otvori artikal Kafa Etiopija 250 g' }))

    // Assert
    expect(await screen.findByTestId('detail')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe(`/items/${kafa.id}`)
  })
})
