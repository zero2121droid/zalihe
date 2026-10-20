import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { onPhone } from '../../test/phone'
import { mockApi, renderRoutes } from '../../test/render'
import { OrdersPage } from './OrdersPage'

const routes = [
  { path: '/orders', element: <OrdersPage /> },
  { path: '/channels/:id/import', element: <div data-testid="import" /> },
]

const channelId = '0199f0aa-0000-7000-8000-000000000001'

const coffee = {
  externalProductId: '10',
  name: 'Kafa Etiopija',
  state: 'linked',
  item: { id: '0199f0aa-0000-7000-8000-0000000000aa', name: 'Kafa Etiopija 250 g', sku: 'KF-ETI-250', unit: 'kom' },
  orderedQuantity: 2,
  appliedQuantity: 2,
}

const order = (overrides: object = {}) => ({
  id: crypto.randomUUID(),
  number: '1041',
  channelId,
  shopUrl: 'https://zrno.rs',
  status: 'processing',
  takesStock: true,
  placedAt: '2026-10-16T09:00:00+00:00',
  unlinkedLineCount: 0,
  lines: [coffee],
  ...overrides,
})

const page = (items: object[], totalCount = items.length) => ({ status: 200, body: { items, totalCount, page: 1, pageSize: 20 } })

describe('OrdersPage', () => {
  it('Render_OnPhone_ShowsEachOrderInOneCellWithStatusAndProducts', async () => {
    // Arrange
    onPhone()
    mockApi({ 'GET /api/orders?': page([order({ status: 'cancelled', takesStock: false })]) })

    // Act
    await renderRoutes(routes, '/orders')

    // Assert
    const row = (await screen.findByRole('button', { name: 'Otvori porudžbinu 1041' })).closest('tr')!
    expect(within(row).getAllByRole('cell')).toHaveLength(1)
    expect(within(row).getByText('Otkazana · Ne skida sa stanja')).toBeInTheDocument()
    expect(within(row).getByText(/Kafa Etiopija/)).toHaveTextContent('Kafa Etiopija ×2')
    expect(screen.queryByRole('columnheader')).not.toBeInTheDocument()
  })

  it('Render_OrdersExist_ShowsNumberStatusAndProducts', async () => {
    // Arrange
    const tea = { externalProductId: '99', name: 'Čaj od nane', state: 'unlinked', item: null, orderedQuantity: 4, appliedQuantity: 0 }
    const third = { ...coffee, externalProductId: '11', name: 'Espresso' }
    mockApi({
      'GET /api/orders?': page([
        order({ lines: [coffee, tea, third], unlinkedLineCount: 1 }),
        order({ number: '1040', status: 'cancelled', takesStock: false }),
      ]),
    })

    // Act
    await renderRoutes(routes, '/orders')

    // Assert
    expect(await screen.findByRole('button', { name: 'Otvori porudžbinu 1041' })).toHaveTextContent('#1041')
    const table = within(screen.getByRole('table'))
    expect(table.getByText('U obradi')).toBeInTheDocument()
    expect(table.getByText('Otkazana')).toBeInTheDocument()
    expect(table.getByText('Ne skida sa stanja')).toBeInTheDocument()
    expect(table.getByText('Čaj od nane')).toBeInTheDocument()
    expect(table.queryByText('Espresso')).not.toBeInTheDocument()
    expect(table.getByText('još 1 proizvod')).toBeInTheDocument()
    expect(table.getByText('1 nepovezan')).toBeInTheDocument()
  })

  it('UnlinkedPill_Clicked_RequestsOrdersWithUnlinkedProducts', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/orders?': page([order()]) })
    const { router } = await renderRoutes(routes, '/orders')
    await screen.findByText('#1041')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Nepovezani proizvodi' }))

    // Assert
    await waitFor(() => expect(router.state.location.search).toBe('?unlinked=1'))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/orders?unlinkedOnly=true&page=1&pageSize=20', expect.anything()))
  })

  it('Search_Typed_RequestsFilteredListFromServer', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/orders?': page([]) })
    await renderRoutes(routes, '/orders')
    await screen.findByText('Još nema porudžbina')

    // Act
    await userEvent.type(screen.getByRole('searchbox', { name: 'Pretraga porudžbina' }), '1041')

    // Assert
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/orders?search=1041&page=1&pageSize=20', expect.anything()))
    expect(await screen.findByText('Nema porudžbina za „1041”.')).toBeInTheDocument()
  })

  it('Row_Opened_ShowsLinesAndHowToLinkUnlinkedProducts', async () => {
    // Arrange
    const tea = { externalProductId: '99', name: null, state: 'unlinked', item: null, orderedQuantity: 4, appliedQuantity: 0 }
    mockApi({ 'GET /api/orders?': page([order({ lines: [coffee, tea], unlinkedLineCount: 1 })]) })
    const { router } = await renderRoutes(routes, '/orders')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Otvori porudžbinu 1041' }))

    // Assert
    const dialog = await screen.findByRole('dialog', { name: 'Porudžbina #1041' })
    expect(within(dialog).getByRole('link', { name: 'Kafa Etiopija 250 g' })).toBeInTheDocument()
    expect(within(dialog).getByText('Proizvod #99')).toBeInTheDocument()
    expect(within(dialog).getByText('Nije povezan')).toBeInTheDocument()

    await userEvent.click(within(dialog).getByRole('link', { name: 'Poveži proizvode' }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/channels/${channelId}/import`))
  })

  it('Row_OpenedForOrderPlacedBeforeLinking_ExplainsWhyNoStockWasTaken', async () => {
    // Arrange
    const linkedLater = { ...coffee, state: 'linkedAfterOrder', appliedQuantity: 0 }
    mockApi({ 'GET /api/orders?': page([order({ status: 'completed', lines: [linkedLater] })]) })
    await renderRoutes(routes, '/orders')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Otvori porudžbinu 1041' }))

    // Assert
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Povezan posle porudžbine')).toBeInTheDocument()
    expect(within(dialog).getByText(/stanje pri povezivanju ju je već uračunalo/)).toBeInTheDocument()
    expect(within(dialog).queryByRole('link', { name: 'Poveži proizvode' })).not.toBeInTheDocument()
  })
})
