import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../../test/render'
import { ReportsPage } from './ReportsPage'

const routes = [{ path: '/reports', element: <ReportsPage /> }]

const summary = { status: 200, body: { stockValue: 1284350, itemsWithoutPrice: 3, slowMoverCount: 4, slowMoverValue: 48200 } }
const page = (items: object[], pageNumber = 1, pageSize = 20) => ({
  status: 200,
  body: { items, totalCount: items.length + (pageNumber - 1) * pageSize, page: pageNumber, pageSize },
})

const coffee = { itemId: 'a', name: 'Kafa Etiopija 250 g', sku: 'KF-ETI-250', unit: 'kom' }

describe('ReportsPage', () => {
  it('Render_SecondPageOfTopSellers_ShowsFiguresAndRanksFromThePage', async () => {
    // Arrange
    mockApi({
      'GET /api/reports/summary': summary,
      'GET /api/items/categories': { status: 200, body: [] },
      'GET /api/reports/top-sellers?page=2': page([{ ...coffee, sold: 38, stock: 4, status: 'low' }], 2),
    })

    // Act
    await renderRoutes(routes, '/reports?page=2')

    // Assert
    const table = await screen.findByRole('table')
    const row = within(table).getAllByRole('row')[1]
    expect(within(row).getByText('21')).toBeInTheDocument()
    expect(within(row).getByRole('link', { name: 'Otvori artikal Kafa Etiopija 250 g' })).toHaveAttribute('href', '/items/a')
    expect(within(row).getByText('Ispod minimuma')).toBeInTheDocument()
    expect(screen.getByText('1.284.350')).toBeInTheDocument()
    expect(screen.getByText('48.200')).toBeInTheDocument()
  })

  it('ChooseReport_SlowMovers_LoadsThemAndShowsItemsNeverSold', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/reports/summary': summary,
      'GET /api/items/categories': { status: 200, body: [] },
      'GET /api/reports/top-sellers': page([]),
      'GET /api/reports/slow-movers': page([
        { ...coffee, stock: 12, value: 10800, lastSoldAt: null },
        { ...coffee, itemId: 'b', name: 'Džezva', sku: 'DZ-1', stock: 2, value: null, lastSoldAt: '2026-07-01T10:00:00Z' },
      ]),
    })
    await renderRoutes(routes, '/reports')
    expect(await screen.findByText('Nije bilo prodaje u poslednjih 30 dana.')).toBeInTheDocument()

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Roba koja stoji' }))

    // Assert
    expect(await screen.findByText('Nikad')).toBeInTheDocument()
    expect(screen.getByText('10.800')).toBeInTheDocument()
    expect(screen.getByText('Bez nabavne cene', { selector: 'p' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url).startsWith('/api/reports/slow-movers'))).toBe(true)
  })

  it('Render_ValueByCategory_ShowsShareOfTotalWithoutSearch', async () => {
    // Arrange
    mockApi({
      'GET /api/reports/summary': { status: 200, body: { ...summary.body, stockValue: 1000 } },
      'GET /api/items/categories': { status: 200, body: ['Kafa'] },
      'GET /api/reports/stock-value': page([
        { category: 'Kafa', itemCount: 12, value: 750, itemsWithoutPrice: 2 },
        { category: null, itemCount: 3, value: 250, itemsWithoutPrice: 0 },
      ]),
    })

    // Act
    await renderRoutes(routes, '/reports?r=stockValue&cat=Kafa')

    // Assert
    expect(await screen.findByText('75%')).toBeInTheDocument()
    expect(screen.getByText('25%')).toBeInTheDocument()
    expect(screen.getByText('Bez kategorije')).toBeInTheDocument()
    expect(screen.getByText('2 artikla bez nabavne cene')).toBeInTheDocument()
    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument()
  })

  it('Render_ApiWithoutReports_SaysWhatFailedInsteadOfBlankFigures', async () => {
    // Arrange: an API started before reports existed answers 404 to all of them.
    const missing = { status: 404, body: { status: 404, code: 'not_found' } }
    mockApi({
      'GET /api/reports/summary': missing,
      'GET /api/items/categories': { status: 200, body: [] },
      'GET /api/reports/top-sellers': missing,
    })

    // Act
    await renderRoutes(routes, '/reports')

    // Assert
    await waitFor(() => expect(screen.getAllByRole('alert')).toHaveLength(2))
    expect(screen.queryByRole('region', { name: 'Pregled zaliha' })).not.toBeInTheDocument()
  })
})
