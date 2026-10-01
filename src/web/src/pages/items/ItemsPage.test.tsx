import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderRoutes } from '../../test/render'
import { ItemsPage } from './ItemsPage'

const routes = [{ path: '/items', element: <ItemsPage /> }]

const item = (overrides: object) => ({
  id: crypto.randomUUID(),
  name: 'Kafa Etiopija 250 g',
  sku: 'KF-ETI-250',
  barcode: null,
  unit: 'kom',
  category: 'Kafa · zrno',
  groupName: null,
  purchasePrice: null,
  salePrice: null,
  minStock: 10,
  isActive: true,
  ...overrides,
})

describe('ItemsPage', () => {
  it('Render_ItemsExist_ShowsRowsAndRange', async () => {
    // Arrange
    mockFetch({
      status: 200,
      body: {
        items: [item({}), item({ name: 'Espresso mešavina', sku: 'KF-ESP-1000', unit: 'kg', minStock: 12.5 })],
        totalCount: 42,
        page: 1,
        pageSize: 20,
      },
    })

    // Act
    await renderRoutes(routes, '/items')

    // Assert
    expect(await screen.findByText('Kafa Etiopija 250 g')).toBeInTheDocument()
    expect(screen.getByText('KF-ESP-1000')).toBeInTheDocument()
    expect(screen.getByText('12,5')).toBeInTheDocument()
    expect(screen.getByText('1–20 od 42')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Prethodna strana' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Sledeća strana' })).toBeEnabled()
  })

  it('Render_NoItems_ShowsEmptyState', async () => {
    // Arrange
    mockFetch({ status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 20 } })

    // Act
    await renderRoutes(routes, '/items')

    // Assert
    expect(await screen.findByText('Još nema artikala')).toBeInTheDocument()
  })

  it('Search_Typed_RequestsFilteredListFromServer', async () => {
    // Arrange
    const empty = { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 20 } }
    const fetchMock = mockFetch(empty, empty)
    const { router } = await renderRoutes(routes, '/items')
    await screen.findByText('Još nema artikala')

    // Act
    await userEvent.type(screen.getByRole('searchbox', { name: 'Pretraga artikala' }), 'etiop')

    // Assert
    await waitFor(() => expect(router.state.location.search).toBe('?q=etiop'))
    await waitFor(() => expect(fetchMock).toHaveBeenLastCalledWith('/api/items?search=etiop&page=1&pageSize=20', expect.anything()))
    expect(await screen.findByText('Nema artikala za „etiop”.')).toBeInTheDocument()
  })

  it('Render_InactiveItem_ShowsInactiveLabel', async () => {
    // Arrange
    mockFetch({ status: 200, body: { items: [item({ isActive: false })], totalCount: 1, page: 1, pageSize: 20 } })

    // Act
    await renderRoutes(routes, '/items?inactive=1')

    // Assert
    expect(await screen.findByText('Neaktivan')).toBeInTheDocument()
  })

  it('ShowInactive_Toggled_RequestsInactiveItems', async () => {
    // Arrange
    const list = { status: 200, body: { items: [item({})], totalCount: 1, page: 1, pageSize: 20 } }
    const fetchMock = mockFetch(list, list)
    const { router } = await renderRoutes(routes, '/items')
    await screen.findByText('Kafa Etiopija 250 g')

    // Act
    await userEvent.click(screen.getByRole('switch', { name: 'Prikaži neaktivne' }))

    // Assert
    await waitFor(() => expect(router.state.location.search).toBe('?inactive=1'))
    await waitFor(() =>
      expect(fetchMock).toHaveBeenLastCalledWith('/api/items?includeInactive=true&page=1&pageSize=20', expect.anything()),
    )
  })

  it('ItemName_Clicked_OpensEditForm', async () => {
    // Arrange
    mockFetch({ status: 200, body: { items: [item({})], totalCount: 1, page: 1, pageSize: 20 } }, { status: 200, body: [] })
    await renderRoutes(routes, '/items')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Izmeni artikal Kafa Etiopija 250 g' }))

    // Assert
    expect(await screen.findByText('Izmena artikla')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Šifra/)).toHaveValue('KF-ETI-250')
  })
})
