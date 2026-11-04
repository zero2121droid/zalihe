import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderPage, testUser } from '../../test/render'
import { ItemModal } from './ItemModal'

// Canvas doesn't exist in jsdom; the shrinking itself is the browser's.
const shrunk = new Blob(['mala'], { type: 'image/webp' })
vi.mock('../../lib/shrinkImage', async (original) => ({
  ...(await original<typeof import('../../lib/shrinkImage')>()),
  shrinkImage: async () => shrunk,
}))

const photo = new File(['velika fotografija'], 'IMG_2041.jpg', { type: 'image/jpeg' })
const pickPhoto = () => userEvent.upload(document.querySelector<HTMLInputElement>('input[type="file"]')!, photo)
const saveItem = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj artikal' }))
}

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

const categories = { 'GET /api/items/categories': { status: 200, body: [] } }

function bodyOf(fetchMock: ReturnType<typeof mockApi>, url: string) {
  const call = fetchMock.mock.calls.find(([u, init]) => u === url && init?.method === 'POST')
  return JSON.parse(call?.[1]?.body as string)
}

describe('ItemModal', () => {
  beforeEach(() => {
    URL.createObjectURL = () => 'blob:slika'
    URL.revokeObjectURL = () => {}
  })

  it('Submit_NewItemWithPicture_CreatesTheItemThenSendsTheShrunkPicture', async () => {
    // Arrange
    const onClose = vi.fn()
    const fetchMock = mockApi({
      ...categories,
      'POST /api/items': { status: 201, body: createdItem },
      'PUT /api/items/1/image': { status: 200, body: createdItem },
    })
    await renderPage(<ItemModal opened onClose={onClose} />)

    // Act
    await pickPhoto()
    await screen.findByRole('button', { name: 'Promeni sliku' })
    await saveItem()

    // Assert
    await waitFor(() => expect(onClose).toHaveBeenCalled())
    const writes = fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST' || init?.method === 'PUT')
    expect(writes.map(([url]) => url)).toEqual(['/api/items', '/api/items/1/image'])
    expect((writes[1][1]!.body as FormData).get('file')).toBeInstanceOf(Blob)
  })

  it('Submit_PictureRemovedBeforeSaving_SendsNoPicture', async () => {
    // Arrange
    const onClose = vi.fn()
    const fetchMock = mockApi({ ...categories, 'POST /api/items': { status: 201, body: createdItem } })
    await renderPage(<ItemModal opened onClose={onClose} />)
    await pickPhoto()

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Ukloni sliku' }))
    await saveItem()

    // Assert
    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(false)
  })

  it('Submit_PictureFails_SaysTheItemIsSavedAndOffersNoSecondSave', async () => {
    // Arrange
    const onClose = vi.fn()
    const fetchMock = mockApi({
      ...categories,
      'POST /api/items': { status: 201, body: createdItem },
      'PUT /api/items/1/image': { status: 500, body: { status: 500, code: 'common.unexpected' } },
    })
    await renderPage(<ItemModal opened onClose={onClose} />)
    await pickPhoto()
    await screen.findByRole('button', { name: 'Promeni sliku' })

    // Act
    await saveItem()

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Artikal je sačuvan, ali slika nije.')
    expect(screen.getByRole('link', { name: 'Otvori artikal' })).toHaveAttribute('href', '/items/1')
    expect(screen.queryByRole('button', { name: 'Sačuvaj artikal' })).not.toBeInTheDocument()
    expect(onClose).not.toHaveBeenCalled()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
  })

  it('Open_ExistingItem_LeavesThePictureToTheItemsPage', async () => {
    // Arrange
    mockApi(categories)

    // Act
    await renderPage(<ItemModal opened onClose={() => {}} item={{ ...createdItem, imageUrl: null, hasOwnImage: false } as never} />)

    // Assert
    expect(await screen.findByLabelText(/^Naziv/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Dodaj sliku' })).not.toBeInTheDocument()
  })

  it('Submit_DecimalsWithComma_SendsNumbers', async () => {
    // Arrange
    const fetchMock = mockApi({ ...categories, 'POST /api/items': { status: 201, body: createdItem } })
    await renderPage(<ItemModal opened onClose={() => {}} />)

    // Act
    // Name and SKU are pasted: only the number fields need typing, and fewer keystrokes keep it quick.
    await userEvent.click(screen.getByLabelText(/^Naziv/))
    await userEvent.paste('Espresso mešavina')
    await userEvent.click(screen.getByLabelText(/^Šifra/))
    await userEvent.paste('KF-ESP-1000')
    await userEvent.type(screen.getByLabelText('Minimalna zaliha'), '12,5')
    await userEvent.type(await screen.findByLabelText('Nabavna cena'), '2.000,00')
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
    const fetchMock = mockApi(categories)
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
    mockApi({
      ...categories,
      'POST /api/items': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'item.sku_duplicate', field: 'sku' }] },
      },
    })
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
    mockApi(categories)

    // Act
    await renderPage(<ItemModal opened onClose={() => {}} item={createdItem as never} />)

    // Assert
    expect(screen.getByText('Izmena artikla')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Naziv/)).toHaveValue('Espresso mešavina')
    expect(screen.getByLabelText('Minimalna zaliha')).toHaveValue('12,5')
    expect(await screen.findByLabelText('Nabavna cena')).toHaveValue('2000')
  })

  it('Submit_ExistingItem_SendsPutForThatItem', async () => {
    // Arrange
    const fetchMock = mockApi({ ...categories, 'PUT /api/items/1': { status: 200, body: createdItem } })
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
    const fetchMock = mockApi({ ...categories, 'POST /api/items/1/deactivate': { status: 204 } })
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
    mockApi(categories)

    // Act
    await renderPage(<ItemModal opened onClose={() => {}} item={{ ...createdItem, isActive: false } as never} />)

    // Assert
    expect(screen.getByRole('button', { name: 'Aktiviraj' })).toBeInTheDocument()
    expect(screen.getByText(/Artikal je neaktivan/)).toBeInTheDocument()
  })

  it('Open_EmployeeWithoutPrices_LeavesOutPurchasePrice', async () => {
    // Arrange
    const fetchMock = mockApi({
      ...categories,
      'GET /api/auth/me': { status: 200, body: { ...testUser, role: 'employee', canSeePurchasePrices: false } },
    })

    // Act
    await renderPage(<ItemModal opened onClose={() => {}} item={{ ...createdItem, purchasePrice: null } as never} />)

    // Assert: once the user is known.
    await waitFor(() => expect(fetchMock.mock.results.some((r, i) => fetchMock.mock.calls[i][0] === '/api/auth/me' && r.type === 'return')).toBe(true))
    await screen.findByLabelText('Prodajna cena')
    expect(screen.queryByLabelText('Nabavna cena')).not.toBeInTheDocument()
  })
})
