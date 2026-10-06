import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
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

  it('Import_OneProductUnchecked_SendsOnlyCheckedAndShowsResult', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/channels/c1/products/preview': { status: 200, body: preview },
      'POST /api/channels/c1/products/import': { status: 200, body: { linkedCount: 1, createdCount: 1, skippedCount: 1 } },
    })
    await render()

    // Act
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Majica basic – M, Siva' }))
    await userEvent.click(screen.getByRole('button', { name: 'Uvezi' }))

    // Assert
    expect(await screen.findByText('Uvoz je završen')).toBeInTheDocument()
    expect(screen.getByText('Napravljeno novih artikala: 1')).toBeInTheDocument()
    expect(screen.getByText('Povezano sa postojećim artiklima: 1')).toBeInTheDocument()
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(post?.[1]?.body as string)).toEqual({ createExternalIds: ['11'] })
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
      'GET /api/channels/c1/products/preview': { status: 200, body: { linkedCount: 9, toLink: [], toCreate: [], skipped: [] } },
    })

    // Act
    await render()

    // Assert
    expect(await screen.findByText('Svi proizvodi iz prodavnice su već povezani.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Uvezi' })).not.toBeInTheDocument()
  })
})
