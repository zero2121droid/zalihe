import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { withReducedMotion } from '../../test/motion'
import { mockApi, renderRoutes } from '../../test/render'
import { ProductImportPage } from './ProductImportPage'

const preview = {
  linkedCount: 2,
  toLink: [{ externalId: '10', name: 'Kafa Etiopija', sku: 'KF-ETI-250', itemId: 'i1', itemName: 'Etiopija 250 g' }],
  toCreate: [
    { externalId: '11', name: 'Ručni mlin', groupName: null, sku: 'ML-RUC-01', category: 'Oprema', price: 9900, stock: 6 },
    { externalId: '51', name: 'Majica basic – M, Siva', groupName: 'Majica basic', sku: 'MAJ-M-SI', category: null, price: null, stock: 5 },
  ],
  skipped: [{ externalId: '12', name: 'Bez šifre', sku: null, error: { code: 'channel.product_no_sku', field: 'sku', params: null } }],
  updates: [],
}

/** Everything linked; the coffee has no barcode in Lagerko and another price than in the shop. */
const differences = {
  linkedCount: 9,
  toLink: [],
  toCreate: [],
  skipped: [],
  updates: [
    {
      externalId: '10',
      itemId: 'i1',
      itemName: 'Etiopija 250 g',
      sku: 'KF-ETI-250',
      fields: [
        { field: 'barcode', current: null, shop: '8600123456789', fillsEmpty: true, largeChange: false },
        { field: 'salePrice', current: '4000', shop: '500', fillsEmpty: false, largeChange: true },
      ],
    },
  ],
}

function render() {
  return renderRoutes(
    [
      { path: '/channels/:id/import', element: <ProductImportPage /> },
      { path: '*', element: <div data-testid="navigated" /> },
    ],
    '/channels/c1/import',
  )
}

describe('ProductImportPage', () => {
  it('Preview_ShopProducts_ShowsFiguresNewLinkedAndSkippedWithReason', async () => {
    // Arrange
    mockApi({ 'GET /api/channels/c1/products/preview': { status: 200, body: preview } })

    // Act
    await render()

    // Assert
    const create = await screen.findByRole('region', { name: 'Novi artikli' })
    expect(within(create).getByText('Ručni mlin')).toBeInTheDocument()
    expect(within(create).getByText('9.900', { exact: false })).toHaveTextContent('9.900 RSD')
    expect(within(create).getByText('—')).toBeInTheDocument()
    const link = screen.getByRole('region', { name: 'Povezivanje sa postojećim artiklima' })
    expect(within(link).getByRole('link', { name: 'Etiopija 250 g' })).toHaveAttribute('href', '/items/i1')
    const skipped = screen.getByRole('region', { name: 'Proizvodi koji se preskaču' })
    expect(within(skipped).getByText(/Proizvod nema šifru/)).toBeInTheDocument()
    expect(screen.getByText('Novi artikli: 2 · Povezivanje: 1')).toBeInTheDocument()
  })

  it('Import_Running_ShowsAnimationOverLockedList', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/channels/c1/products/preview': { status: 200, body: preview } })
    const answer = fetchMock.getMockImplementation()!
    fetchMock.mockImplementation((input, init) => (init?.method === 'POST' ? new Promise<Response>(() => {}) : answer(input, init)))
    await render()

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Uvezi' }))

    // Assert
    expect(await screen.findByText('Uvozim proizvode…')).toBeInTheDocument()
    expect(screen.getByText('Ručni mlin').closest('[inert]')).not.toBeNull()
    expect(screen.getByText('Uvozim proizvode…').closest('[inert]')).toBeNull()
  })

  it('Import_Finished_CountsUpThenClosesWithX', async () => {
    // Arrange
    withReducedMotion()
    mockApi({
      'GET /api/channels/c1/products/preview': { status: 200, body: preview },
      'POST /api/channels/c1/products/import': { status: 200, body: { linkedCount: 1, createdCount: 2, skippedCount: 0 } },
    })
    await render()

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Uvezi' }))
    const close = await screen.findByRole('button', { name: 'Zatvori' })

    // Assert: the overlay counted to the last number and the list behind it is still locked
    expect(screen.getByTestId('import-count')).toHaveTextContent('3 / 3')
    expect(screen.getByText('Ručni mlin').closest('[inert]')).not.toBeNull()

    // Act
    await userEvent.click(close)

    // Assert
    expect(screen.queryByTestId('import-count')).toBeNull()
    expect(screen.getByRole('link', { name: 'Idi na artikle' })).toBeInTheDocument()
  })

  it('Import_OneProductUnchecked_SendsOnlyCheckedAndShowsResult', async () => {
    // Arrange
    withReducedMotion()
    const fetchMock = mockApi({
      'GET /api/channels/c1/products/preview': { status: 200, body: preview },
      'POST /api/channels/c1/products/import': { status: 200, body: { linkedCount: 1, createdCount: 1, skippedCount: 1 } },
    })
    await render()

    // Act
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Majica basic – M, Siva' }))
    await userEvent.click(screen.getByRole('button', { name: 'Uvezi' }))

    // Assert
    await userEvent.click(await screen.findByRole('button', { name: 'Zatvori' }))
    expect(screen.getByText('Uvoz je završen')).toBeInTheDocument()
    expect(screen.getByText('Napravljeno novih artikala: 1')).toBeInTheDocument()
    expect(screen.getByText('Povezano sa postojećim artiklima: 1')).toBeInTheDocument()
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(post?.[1]?.body as string)).toEqual({ createExternalIds: ['11'], applyUpdates: [] })
  })

  it('SelectAll_Unchecked_LeavesOnlyLinking', async () => {
    // Arrange
    mockApi({ 'GET /api/channels/c1/products/preview': { status: 200, body: preview } })
    await render()

    // Act
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Izaberi sve' }))

    // Assert
    expect(screen.getByText('Novi artikli: 0 · Povezivanje: 1')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Uvezi' })).toBeEnabled()
  })

  it('Preview_ShopUnreachable_ShowsReasonAndRetry', async () => {
    // Arrange
    mockApi({
      'GET /api/channels/c1/products/preview': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'channel.unreachable', field: null }] },
      },
    })

    // Act
    await render()

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Prodavnica nije dostupna')
    expect(screen.getByRole('button', { name: 'Pokušaj ponovo' })).toBeInTheDocument()
  })

  it('Preview_EverythingLinked_SaysSoWithoutImportButton', async () => {
    // Arrange
    mockApi({
      'GET /api/channels/c1/products/preview': { status: 200, body: { linkedCount: 9, toLink: [], toCreate: [], skipped: [], updates: [] } },
    })

    // Act
    await render()

    // Assert
    expect(await screen.findByText('Svi proizvodi iz prodavnice su već povezani.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Uvezi' })).not.toBeInTheDocument()
  })

  it('RefreshImages_EverythingLinked_ImportsNothingNewAndSaysHowManyHaveImages', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/channels/c1/products/preview': { status: 200, body: { linkedCount: 9, toLink: [], toCreate: [], skipped: [], updates: [] } },
      'POST /api/channels/c1/products/import': {
        status: 200,
        body: { linkedCount: 0, createdCount: 0, skippedCount: 0, imageCount: 7 },
      },
    })
    await render()

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Osveži slike iz prodavnice' }))

    // Assert
    expect(await screen.findByRole('status')).toHaveTextContent('Slike su osvežene: 7 proizvoda ima sliku.')
    const [, init] = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/products/import'))!
    expect(JSON.parse(String(init!.body))).toEqual({ createExternalIds: [] })
  })

  it('Preview_Differences_ChecksOnlyWhatFillsAnEmptyFieldAndWarnsOfLargeChange', async () => {
    // Arrange
    mockApi({ 'GET /api/channels/c1/products/preview': { status: 200, body: differences } })

    // Act
    await render()

    // Assert
    const section = await screen.findByRole('region', { name: 'Razlike sa prodavnicom' })
    expect(within(section).getByRole('checkbox', { name: /Barkod: prazno → 8600123456789/ })).toBeChecked()
    const price = within(section).getByRole('checkbox', { name: /Prodajna cena: 4\.000 RSD → 500 RSD/ })
    expect(price).not.toBeChecked()
    expect(price.closest('.mantine-Checkbox-root')).toHaveTextContent('velika razlika')
  })

  it('ApplyChanges_OnlyBarcodeChosen_SendsOnlyBarcode', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/channels/c1/products/preview': { status: 200, body: differences },
      'POST /api/channels/c1/products/import': {
        status: 200,
        body: { linkedCount: 0, createdCount: 0, skippedCount: 0, imageCount: 7, updatedCount: 1 },
      },
    })
    await render()

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Primeni izabrane izmene' }))

    // Assert
    const [, init] = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/products/import'))!
    expect(JSON.parse(String(init!.body))).toEqual({ createExternalIds: [], applyUpdates: [{ externalId: '10', field: 'barcode' }] })
    expect(await screen.findByText('Izmenjen je 1 artikal.')).toBeInTheDocument()
  })
})
