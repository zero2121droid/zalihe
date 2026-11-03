import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderPage } from '../../test/render'
import { NewOrderModal } from './NewOrderModal'

const item = (overrides: object = {}) => ({
  id: 'item-1',
  name: 'Kafa Etiopija 250 g',
  sku: 'KF-ETI-250',
  barcode: null,
  unit: 'kom',
  category: null,
  groupName: null,
  purchasePrice: null,
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

const items = { status: 200, body: { items: [item(), item({ id: 'item-2', name: 'Filter papir V60', sku: 'FP-V60', stock: 30 })], totalCount: 2, page: 1, pageSize: 8 } }

const created = {
  status: 201,
  body: {
    id: 'o1', number: 'R-0001', source: 'manual', channelId: null, shopUrl: null, reference: null, note: null,
    status: 'confirmed', takesStock: true, placedAt: '2026-11-03T12:00:00Z', unlinkedLineCount: 0, lines: [],
  },
}

function postedBody(fetchMock: ReturnType<typeof mockApi>) {
  const call = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
  return JSON.parse(call?.[1]?.body as string)
}

async function addItem(name: string) {
  await userEvent.type(screen.getByLabelText('Dodaj artikal', { selector: 'input' }), 'ka')
  await userEvent.click(await screen.findByRole('option', { name: new RegExp(name), hidden: true }))
}

describe('NewOrderModal', () => {
  it('Submit_TwoItems_SendsLinesReferenceAndNote', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/items?': items, 'POST /api/orders': created })
    let closed = false
    await renderPage(<NewOrderModal opened onClose={() => (closed = true)} />)

    // Act
    await addItem('Kafa Etiopija')
    await addItem('Filter papir')
    const coffee = screen.getByLabelText('Količina za Kafa Etiopija 250 g')
    await userEvent.clear(coffee)
    await userEvent.type(coffee, '2,5')
    await userEvent.type(screen.getByLabelText('Referenca'), 'IG 154')
    await userEvent.type(screen.getByLabelText(/^Napomena/), 'Lično preuzimanje')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj porudžbinu' }))

    // Assert
    await waitFor(() => expect(closed).toBe(true))
    expect(postedBody(fetchMock)).toEqual({
      lines: [
        { itemId: 'item-1', quantity: 2.5 },
        { itemId: 'item-2', quantity: 1 },
      ],
      reference: 'IG 154',
      note: 'Lično preuzimanje',
    })
  })

  it('Add_SameItemTwice_AddsOneToItsLine', async () => {
    // Arrange
    mockApi({ 'GET /api/items?': items })
    await renderPage(<NewOrderModal opened onClose={() => {}} />)

    // Act
    await addItem('Kafa Etiopija')
    await addItem('Kafa Etiopija')

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(1)
    expect(screen.getByLabelText('Količina za Kafa Etiopija 250 g')).toHaveValue('2')
  })

  it('Type_QuantityBeyondStock_WarnsButAllows', async () => {
    // Arrange
    mockApi({ 'GET /api/items?': items })
    await renderPage(<NewOrderModal opened onClose={() => {}} />)
    await addItem('Kafa Etiopija')

    // Act
    const coffee = screen.getByLabelText('Količina za Kafa Etiopija 250 g')
    await userEvent.clear(coffee)
    await userEvent.type(coffee, '5')

    // Assert
    expect(screen.getByText(/Stanje nekih artikala će otići u minus/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sačuvaj porudžbinu' })).toBeEnabled()
  })

  it('Submit_NoItems_AsksForOneWithoutCallingApi', async () => {
    // Arrange
    const fetchMock = mockApi({})
    await renderPage(<NewOrderModal opened onClose={() => {}} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj porudžbinu' }))

    // Assert
    expect(await screen.findByText('Dodaj bar jedan artikal.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('Submit_QuantityZero_ShowsErrorAtThatLine', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/items?': items })
    await renderPage(<NewOrderModal opened onClose={() => {}} />)
    await addItem('Kafa Etiopija')

    // Act
    const coffee = screen.getByLabelText('Količina za Kafa Etiopija 250 g')
    await userEvent.clear(coffee)
    await userEvent.type(coffee, '0')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj porudžbinu' }))

    // Assert
    expect(await screen.findByText('Količina mora biti veća od nule.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })
})
