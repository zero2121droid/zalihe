import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes, testUser } from '../../test/render'
import { NewReceiptPage } from './NewReceiptPage'

const routes = [
  { path: '/receipts/new', element: <NewReceiptPage /> },
  { path: '/receipts', element: <div data-testid="receipts" /> },
]

const item = (overrides: object = {}) => ({
  id: 'item-1',
  name: 'Kafa Etiopija 250 g',
  sku: 'KF-ETI-250',
  barcode: null,
  unit: 'kom',
  category: null,
  groupName: null,
  purchasePrice: 1000,
  salePrice: null,
  minStock: 10,
  isActive: true,
  stock: 4,
  status: 'low',
  stockValue: null,
  sold30Days: 0,
  imageUrl: null,
  hasOwnImage: false,
  ...overrides,
})

const items = {
  status: 200,
  body: { items: [item(), item({ id: 'item-2', name: 'Filter papir V60', sku: 'FP-V60', purchasePrice: null })], totalCount: 2, page: 1, pageSize: 8 },
}

const created = {
  status: 201,
  body: {
    id: 'r1', supplierName: 'Kafa Import', documentNumber: 'F-154', documentDate: '2026-11-02', postedAt: '2026-11-03T12:00:00Z',
    createdByName: 'Miljan', lineCount: 2, totalValue: null, lines: [],
  },
}

function posts(fetchMock: ReturnType<typeof mockApi>) {
  return fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST').map(([, init]) => JSON.parse(init?.body as string))
}

async function addItem(name: string) {
  await userEvent.type(screen.getByLabelText('Dodaj artikal', { selector: 'input' }), 'ka')
  await userEvent.click(await screen.findByRole('option', { name: new RegExp(name), hidden: true }))
}

async function fillHeader() {
  await userEvent.type(screen.getByLabelText(/^Dobavljač/), 'Kafa Import')
  await userEvent.type(screen.getByLabelText('Broj fakture ili otpremnice'), 'F-154')
  const date = screen.getByLabelText(/^Datum dokumenta/)
  await userEvent.clear(date)
  await userEvent.type(date, '2026-11-02')
}

describe('NewReceiptPage', () => {
  it('Submit_TwoLines_SendsPricesAndOnlyChosenPriceUpdates', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/items?': items, 'POST /api/receipts': created })
    await renderRoutes(routes, '/receipts/new')
    await fillHeader()
    await addItem('Kafa Etiopija')
    await addItem('Filter papir')

    // Act: the coffee costs more now, but the user keeps the item's price; the filter had none.
    const coffeeQuantity = screen.getByLabelText('Količina za Kafa Etiopija 250 g')
    await userEvent.clear(coffeeQuantity)
    await userEvent.type(coffeeQuantity, '24')
    const coffeePrice = screen.getByLabelText('Nabavna cena za Kafa Etiopija 250 g')
    await userEvent.clear(coffeePrice)
    await userEvent.type(coffeePrice, '1100,50')
    await userEvent.type(screen.getByLabelText('Nabavna cena za Filter papir V60'), '320')
    await userEvent.click(screen.getByRole('button', { name: 'Proknjiži prijem' }))

    // Assert
    await screen.findByTestId('receipts')
    expect(posts(fetchMock)).toEqual([
      {
        supplierName: 'Kafa Import',
        documentNumber: 'F-154',
        documentDate: '2026-11-02',
        allowDuplicate: false,
        lines: [
          { itemId: 'item-1', quantity: 24, unitPrice: 1100.5, updatePurchasePrice: false },
          { itemId: 'item-2', quantity: 1, unitPrice: 320, updatePurchasePrice: true },
        ],
      },
    ])
  })

  it('Type_PriceDiffersFromItems_OffersToUpdateIt', async () => {
    // Arrange
    mockApi({ 'GET /api/items?': items })
    await renderRoutes(routes, '/receipts/new')
    await addItem('Kafa Etiopija')
    expect(screen.queryByLabelText('Postavi kao nabavnu cenu')).not.toBeInTheDocument()

    // Act
    const price = screen.getByLabelText('Nabavna cena za Kafa Etiopija 250 g')
    await userEvent.clear(price)
    await userEvent.type(price, '1100')

    // Assert
    expect(screen.getByLabelText('Postavi kao nabavnu cenu')).not.toBeChecked()
    expect(screen.getByText('sada 1.000 RSD')).toBeInTheDocument()
    expect(screen.getByText('Ukupno')).toBeInTheDocument()
  })

  it('Submit_Empty_ShowsErrorsWithoutCallingApi', async () => {
    // Arrange
    const fetchMock = mockApi({})
    await renderRoutes(routes, '/receipts/new')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Proknjiži prijem' }))

    // Assert
    expect(await screen.findByText('Dodaj bar jedan artikal.')).toBeInTheDocument()
    expect(screen.getByText('Obavezno polje.')).toBeInTheDocument()
    expect(posts(fetchMock)).toEqual([])
  })

  it('Submit_SameDocumentAgain_AsksAndPostsAnywayOnRequest', async () => {
    // Arrange
    let calls = 0
    const fetchMock = mockApi({
      'GET /api/items?': items,
      'POST /api/receipts': () =>
        ++calls === 1
          ? {
              status: 409,
              body: {
                code: 'receipt.duplicate_document',
                errors: [{ code: 'receipt.duplicate_document', field: 'documentNumber', params: { postedAt: '2026-11-01T10:00:00Z' } }],
              },
            }
          : created,
    })
    await renderRoutes(routes, '/receipts/new')
    await fillHeader()
    await addItem('Kafa Etiopija')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Proknjiži prijem' }))
    expect(await screen.findByText(/Ovaj dokument dobavljača je već proknjižen/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Ipak proknjiži' }))

    // Assert
    await screen.findByTestId('receipts')
    expect(posts(fetchMock).map((body) => body.allowDuplicate)).toEqual([false, true])
  })

  it('Render_EmployeeWithoutPrices_HasNoPriceFieldAndSendsNoPrice', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/auth/me': { status: 200, body: { ...testUser, role: 'employee', canSeePurchasePrices: false } },
      'GET /api/items?': { status: 200, body: { items: [item({ purchasePrice: null })], totalCount: 1, page: 1, pageSize: 8 } },
      'POST /api/receipts': created,
    })
    await renderRoutes(routes, '/receipts/new')
    await fillHeader()
    await addItem('Kafa Etiopija')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Proknjiži prijem' }))

    // Assert
    await screen.findByTestId('receipts')
    expect(screen.queryByLabelText(/^Nabavna cena/)).not.toBeInTheDocument()
    await waitFor(() =>
      expect(posts(fetchMock)[0].lines).toEqual([{ itemId: 'item-1', quantity: 1, unitPrice: null, updatePurchasePrice: false }]),
    )
  })
})
