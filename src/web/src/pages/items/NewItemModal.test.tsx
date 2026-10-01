import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage } from '../../test/render'
import { NewItemModal } from './NewItemModal'

const createdItem = {
  id: '1',
  name: 'Espresso mešavina',
  sku: 'KF-ESP-1000',
  barcode: null,
  unit: 'kg',
  category: null,
  groupName: null,
  purchasePrice: 2000,
  salePrice: null,
  minStock: 12.5,
  isActive: true,
}

function bodyOf(fetchMock: ReturnType<typeof mockFetch>, url: string) {
  const call = fetchMock.mock.calls.find(([u, init]) => u === url && init?.method === 'POST')
  return JSON.parse(call?.[1]?.body as string)
}

describe('NewItemModal', () => {
  it('Submit_DecimalsWithComma_SendsNumbers', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 200, body: [] }, { status: 201, body: createdItem })
    await renderPage(<NewItemModal opened onClose={() => {}} />)

    // Act
    await userEvent.type(screen.getByLabelText(/^Naziv/), 'Espresso mešavina')
    await userEvent.type(screen.getByLabelText(/^Šifra/), 'KF-ESP-1000')
    await userEvent.type(screen.getByLabelText('Minimalna zaliha'), '12,5')
    await userEvent.type(screen.getByLabelText('Nabavna cena'), '2.000,00')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj artikal' }))

    // Assert
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true))
    expect(bodyOf(fetchMock, '/api/items')).toEqual({
      name: 'Espresso mešavina',
      sku: 'KF-ESP-1000',
      barcode: null,
      unit: 'kom',
      category: null,
      groupName: null,
      minStock: 12.5,
      purchasePrice: 2000,
      salePrice: null,
    })
  })

  it('Submit_TextInNumberField_ShowsErrorWithoutCallingApi', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 200, body: [] })
    await renderPage(<NewItemModal opened onClose={() => {}} />)

    // Act
    await userEvent.type(screen.getByLabelText('Prodajna cena'), 'skupo')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj artikal' }))

    // Assert
    expect(await screen.findByText('Unesi broj, npr. 12,5.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('Submit_DuplicateSku_ShowsErrorUnderSku', async () => {
    // Arrange
    mockFetch(
      { status: 200, body: [] },
      {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'item.sku_duplicate', field: 'sku' }] },
      },
    )
    await renderPage(<NewItemModal opened onClose={() => {}} />)

    // Act
    await userEvent.type(screen.getByLabelText(/^Naziv/), 'Kafa')
    await userEvent.type(screen.getByLabelText(/^Šifra/), 'KF-1')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj artikal' }))

    // Assert
    expect(await screen.findByText('Artikal sa ovom šifrom već postoji.')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Šifra/)).toHaveAttribute('aria-invalid', 'true')
  })
})
