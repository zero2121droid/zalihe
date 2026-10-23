import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderRoutes } from '../test/render'
import { QuickSearch } from './QuickSearch'

const item = (id: string, name: string, sku: string, stock: number) => ({
  id,
  name,
  sku,
  barcode: null,
  unit: 'kom',
  category: null,
  groupName: null,
  purchasePrice: null,
  salePrice: null,
  minStock: 10,
  isActive: true,
  stock,
  status: stock < 10 ? 'low' : 'inStock',
  stockValue: null,
  sold30Days: 0,
  imageUrl: null,
  hasOwnImage: false,
})

const coffees = {
  status: 200,
  body: {
    items: [item('a', 'Kafa Etiopija 250 g', 'KF-ETI-250', 27), item('b', 'Kafa Kolumbija 250 g', 'KF-KOL-250', 4)],
    totalCount: 2,
    page: 1,
    pageSize: 8,
  },
}

function open(onClose = vi.fn()) {
  return renderRoutes(
    [
      { path: '/', element: <QuickSearch onClose={onClose} /> },
      { path: '/items/:id', element: <div data-testid="item" /> },
    ],
    '/',
  )
}

describe('QuickSearch', () => {
  it('Type_Name_ShowsMatchingItemsWithTheirStock', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/items?': coffees })
    await open()

    // Act
    await userEvent.type(screen.getByRole('combobox', { name: 'Naziv, šifra ili barkod' }), 'kafa')

    // Assert
    expect(await screen.findByRole('option', { name: /Kafa Etiopija 250 g/ })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('option', { name: /Kafa Kolumbija 250 g/ })).toHaveTextContent('Ispod minimuma')
    expect(String(fetchMock.mock.calls.at(-1)![0])).toContain('search=kafa')
  })

  it('ArrowDownThenEnter_OpensSecondItemAndCloses', async () => {
    // Arrange
    mockApi({ 'GET /api/items?': coffees })
    const onClose = vi.fn()
    const { router } = await open(onClose)
    const input = screen.getByRole('combobox', { name: 'Naziv, šifra ili barkod' })
    await userEvent.type(input, 'kafa')
    await screen.findByRole('option', { name: /Kafa Kolumbija/ })

    // Act
    await userEvent.keyboard('{ArrowDown}{Enter}')

    // Assert
    await waitFor(() => expect(router.state.location.pathname).toBe('/items/b'))
    expect(onClose).toHaveBeenCalled()
  })

  it('Type_NothingMatches_SaysSo', async () => {
    // Arrange
    mockApi({ 'GET /api/items?': { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 8 } } })
    await open()

    // Act
    await userEvent.type(screen.getByRole('combobox', { name: 'Naziv, šifra ili barkod' }), 'xyz')

    // Assert
    expect(await screen.findByText('Nema artikla za „xyz”.')).toBeInTheDocument()
  })

  it('ClickClose_Closes', async () => {
    // Arrange
    mockApi({})
    const onClose = vi.fn()
    await open(onClose)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Zatvori' }))

    // Assert
    expect(onClose).toHaveBeenCalled()
  })

  describe('camera', () => {
    const stopTrack = vi.fn()
    const readyState = Object.getOwnPropertyDescriptor(HTMLMediaElement.prototype, 'readyState')!

    /** A camera that shows a barcode, and the browser's own reader that reads it. */
    function cameraShowing(code: string) {
      vi.stubGlobal('navigator', {
        ...navigator,
        mediaDevices: { getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [{ stop: stopTrack }] }) },
      })
      vi.stubGlobal(
        'BarcodeDetector',
        class {
          static getSupportedFormats = () => Promise.resolve(['ean_13', 'code_128'])
          detect = () => Promise.resolve([{ rawValue: code }])
        },
      )
      vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue()
      Object.defineProperty(HTMLMediaElement.prototype, 'readyState', { configurable: true, get: () => 4 })
    }

    afterEach(() => {
      Object.defineProperty(HTMLMediaElement.prototype, 'readyState', readyState)
      vi.unstubAllGlobals()
    })

    it('Scan_BarcodeOfOneItem_OpensItAndTurnsCameraOff', async () => {
      // Arrange
      cameraShowing('2000000000271')
      const scanned = { ...coffees.body.items[1], barcode: '2000000000271' }
      mockApi({ 'GET /api/items?': { status: 200, body: { items: [scanned], totalCount: 1, page: 1, pageSize: 8 } } })
      const onClose = vi.fn()
      const { router } = await open(onClose)

      // Act
      await userEvent.click(screen.getByRole('button', { name: 'Skeniraj barkod kamerom' }))

      // Assert
      await waitFor(() => expect(router.state.location.pathname).toBe('/items/b'))
      expect(onClose).toHaveBeenCalled()
      expect(stopTrack).toHaveBeenCalled()
    })

    it('Scan_CameraNotAllowed_SaysHowToAllowIt', async () => {
      // Arrange
      cameraShowing('x')
      vi.mocked(navigator.mediaDevices.getUserMedia).mockRejectedValue(Object.assign(new Error('no'), { name: 'NotAllowedError' }))
      mockApi({})
      await open()

      // Act
      await userEvent.click(screen.getByRole('button', { name: 'Skeniraj barkod kamerom' }))

      // Assert
      expect(await screen.findByRole('alert')).toHaveTextContent('Kamera nije dozvoljena')
    })
  })
})
