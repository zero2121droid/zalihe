import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage } from '../../test/render'
import { ItemModal } from './ItemModal'

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

describe('ItemModal', () => {
  it('Submit_DecimalsWithComma_SendsNumbers', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 200, body: [] }, { status: 201, body: createdItem })
    await renderPage(<ItemModal opened onClose={() => {}} />)

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
    await renderPage(<ItemModal opened onClose={() => {}} />)

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
    await renderPage(<ItemModal opened onClose={() => {}} />)

    // Act
    await userEvent.type(screen.getByLabelText(/^Naziv/), 'Kafa')
    await userEvent.type(screen.getByLabelText(/^Šifra/), 'KF-1')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj artikal' }))

    // Assert
    expect(await screen.findByText('Artikal sa ovom šifrom već postoji.')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Šifra/)).toHaveAttribute('aria-invalid', 'true')
  })

  it('Open_ExistingItem_ShowsItsDataWithLocalDecimals', async () => {
    // Arrange
    mockFetch({ status: 200, body: [] })

    // Act
    await renderPage(<ItemModal opened onClose={() => {}} item={createdItem as never} />)

    // Assert
    expect(screen.getByText('Izmena artikla')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Naziv/)).toHaveValue('Espresso mešavina')
    expect(screen.getByLabelText('Minimalna zaliha')).toHaveValue('12,5')
    expect(screen.getByLabelText('Nabavna cena')).toHaveValue('2000')
  })

  it('Submit_ExistingItem_SendsPutForThatItem', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 200, body: [] }, { status: 200, body: createdItem })
    await renderPage(<ItemModal opened onClose={() => {}} item={createdItem as never} />)

    // Act
    await userEvent.clear(screen.getByLabelText('Minimalna zaliha'))
    await userEvent.type(screen.getByLabelText('Minimalna zaliha'), '7,25')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj artikal' }))

    // Assert
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(true))
    const [url, init] = fetchMock.mock.calls.find(([, i]) => i?.method === 'PUT')!
    expect(url).toBe('/api/items/1')
    expect(JSON.parse(init?.body as string)).toMatchObject({ sku: 'KF-ESP-1000', unit: 'kg', minStock: 7.25 })
  })

  it('Deactivate_ActiveItem_CallsDeactivateAndCloses', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 200, body: [] }, { status: 204 })
    let closed = false
    await renderPage(<ItemModal opened onClose={() => (closed = true)} item={createdItem as never} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Deaktiviraj' }))

    // Assert
    await waitFor(() => expect(closed).toBe(true))
    expect(fetchMock).toHaveBeenCalledWith('/api/items/1/deactivate', expect.objectContaining({ method: 'POST' }))
  })

  it('Open_InactiveItem_OffersActivateAndExplains', async () => {
    // Arrange
    mockFetch({ status: 200, body: [] })

    // Act
    await renderPage(<ItemModal opened onClose={() => {}} item={{ ...createdItem, isActive: false } as never} />)

    // Assert
    expect(screen.getByRole('button', { name: 'Aktiviraj' })).toBeInTheDocument()
    expect(screen.getByText(/Artikal je neaktivan/)).toBeInTheDocument()
  })
})
