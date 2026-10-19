import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ItemDto } from '../../api/generated/model'
import { mockApi, renderPage } from '../../test/render'
import { ItemImageEditor } from './ItemImageEditor'

// Canvas doesn't exist in jsdom; the shrinking itself is the browser's. A plain function, not vi.fn():
// a vi.fn() that rejects is reported as an unhandled rejection even when the component catches it.
let shrink: (file: Blob) => Promise<Blob>
vi.mock('../../lib/shrinkImage', async (original) => ({
  ...(await original<typeof import('../../lib/shrinkImage')>()),
  shrinkImage: (file: Blob) => shrink(file),
}))

const item: ItemDto = {
  id: 'item-1',
  name: 'Kafa Etiopija 250 g',
  sku: 'KF-ETI-250',
  barcode: null,
  unit: 'kom',
  category: null,
  groupName: null,
  purchasePrice: null,
  salePrice: null,
  minStock: 0,
  isActive: true,
  stock: 4,
  status: 'inStock',
  stockValue: null,
  sold30Days: 0,
  imageUrl: null,
  hasOwnImage: false,
}

const photo = () => new File(['velika fotografija'], 'IMG_2041.jpg', { type: 'image/jpeg' })
const fileInput = () => document.querySelector<HTMLInputElement>('input[type="file"]')!

describe('ItemImageEditor', () => {
  beforeEach(() => {
    shrink = () => Promise.reject(new Error('Not set by the test'))
  })

  it('Pick_Photo_SendsTheShrunkPicture', async () => {
    // Arrange
    const shrunk = new Blob(['mala'], { type: 'image/webp' })
    shrink = async () => shrunk
    const fetchMock = mockApi({
      'PUT /api/items/item-1/image': { status: 200, body: { ...item, imageUrl: '/api/items/item-1/image?v=1', hasOwnImage: true } },
      'GET /api/items': { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 20 } },
    })
    await renderPage(<ItemImageEditor item={item} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Dodaj sliku' }))
    await userEvent.upload(fileInput(), photo())

    // Assert
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url) === '/api/items/item-1/image')).toBe(true))
    const [, init] = fetchMock.mock.calls.find(([url]) => String(url) === '/api/items/item-1/image')!
    expect(init!.method).toBe('PUT')
    expect((init!.body as FormData).get('file')).toBeInstanceOf(Blob)
    expect(((init!.body as FormData).get('file') as Blob).type).toBe('image/webp')
  })

  it('Pick_ServerRefusesTheType_ShowsWhy', async () => {
    // Arrange
    shrink = async () => new Blob(['x'], { type: 'image/jpeg' })
    mockApi({
      'PUT /api/items/item-1/image': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'item.image_unsupported', field: 'file', params: null }] },
      },
    })
    await renderPage(<ItemImageEditor item={item} />)

    // Act
    await userEvent.upload(fileInput(), photo())

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Slika mora biti JPEG, PNG ili WebP.')
  })

  it('Pick_PictureTheBrowserCantOpen_SaysSoWithoutSending', async () => {
    // Arrange
    // The real one: jsdom can't open pictures (no createImageBitmap), like a browser given a HEIC it can't read.
    const actual = await vi.importActual<typeof import('../../lib/shrinkImage')>('../../lib/shrinkImage')
    shrink = actual.shrinkImage
    const fetchMock = mockApi({})
    await renderPage(<ItemImageEditor item={item} />)

    // Act
    await userEvent.upload(fileInput(), photo())

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Ova slika ne može da se otvori.')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('Render_ShopImageOnly_OffersToChangeButNotToRemove', async () => {
    mockApi({})

    await renderPage(<ItemImageEditor item={{ ...item, imageUrl: 'https://zrno.rs/kafa.jpeg', hasOwnImage: false }} />)

    expect(screen.getByRole('button', { name: 'Promeni sliku' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ukloni sliku' })).not.toBeInTheDocument()
  })

  it('Remove_OwnPicture_DeletesIt', async () => {
    // Arrange
    const fetchMock = mockApi({
      'DELETE /api/items/item-1/image': { status: 204 },
      'GET /api/items': { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 20 } },
    })
    await renderPage(<ItemImageEditor item={{ ...item, imageUrl: '/api/items/item-1/image?v=1', hasOwnImage: true }} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Ukloni sliku' }))

    // Assert
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([url, init]) => String(url) === '/api/items/item-1/image' && init?.method === 'DELETE')).toBe(true),
    )
  })
})
