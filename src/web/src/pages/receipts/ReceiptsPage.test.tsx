import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes, testUser } from '../../test/render'
import { ReceiptsPage } from './ReceiptsPage'

const routes = [{ path: '/receipts', element: <ReceiptsPage /> }]

const receipt = (overrides: object = {}) => ({
  id: 'r1',
  supplierName: 'Kafa Import',
  documentNumber: 'F-154',
  documentDate: '2026-11-02',
  postedAt: '2026-11-03T12:00:00Z',
  createdByName: 'Miljan',
  lineCount: 2,
  totalValue: 30012,
  lines: [
    { id: 'l1', item: { id: 'item-1', name: 'Kafa Etiopija 250 g', sku: 'KF-ETI-250', unit: 'kom' }, quantity: 24, unitPrice: 1250.5 },
    { id: 'l2', item: { id: 'item-2', name: 'Filter papir V60', sku: 'FP-V60', unit: 'kom' }, quantity: 2, unitPrice: null },
  ],
  ...overrides,
})

const page = (items: object[]) => ({ status: 200, body: { items, totalCount: items.length, page: 1, pageSize: 20 } })

describe('ReceiptsPage', () => {
  it('Render_Receipts_ShowsSupplierDocumentDateLinesAndValue', async () => {
    // Arrange
    mockApi({ 'GET /api/receipts?': page([receipt(), receipt({ id: 'r2', supplierName: 'Papir d.o.o.', documentNumber: null, totalValue: null })]) })

    // Act
    await renderRoutes(routes, '/receipts')

    // Assert
    const row = within((await screen.findByRole('button', { name: 'Otvori prijem Kafa Import F-154' })).closest('tr')!)
    expect(row.getByText('F-154')).toBeInTheDocument()
    expect(row.getByText('2. 11. 2026.')).toBeInTheDocument()
    // The value column appears once the user's rights have loaded.
    expect(await row.findByText('30.012')).toBeInTheDocument()
    expect(screen.getByText('bez broja')).toBeInTheDocument()
  })

  it('Click_Receipt_OpensItsLinesWithPricesAndTotal', async () => {
    // Arrange
    mockApi({ 'GET /api/receipts?': page([receipt()]) })
    await renderRoutes(routes, '/receipts')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Otvori prijem Kafa Import F-154' }))

    // Assert
    const dialog = within(await screen.findByRole('dialog'))
    expect(dialog.getByText('Prijem F-154')).toBeInTheDocument()
    expect(dialog.getByRole('link', { name: 'Kafa Etiopija 250 g' })).toHaveAttribute('href', '/items/item-1')
    expect(await dialog.findByText('1.250,5')).toBeInTheDocument()
    // The coffee line's value and the receipt's total.
    expect(dialog.getAllByText('30.012')).toHaveLength(2)
    // The filter has no price, so neither price nor value.
    expect(dialog.getAllByText('—')).toHaveLength(2)
  })

  it('Render_JustPosted_OpensTheReceipt', async () => {
    // Arrange
    mockApi({ 'GET /api/receipts?': page([receipt()]) })

    // Act
    await renderRoutes(routes, '/receipts?opened=r1')

    // Assert
    expect(await screen.findByRole('dialog')).toHaveTextContent('Prijem F-154')
  })

  it('Render_EmployeeWithoutPrices_HidesValueColumn', async () => {
    // Arrange
    mockApi({
      'GET /api/auth/me': { status: 200, body: { ...testUser, role: 'employee', canSeePurchasePrices: false } },
      'GET /api/receipts?': page([receipt({ totalValue: null })]),
    })

    // Act
    await renderRoutes(routes, '/receipts')

    // Assert
    await screen.findByRole('button', { name: 'Otvori prijem Kafa Import F-154' })
    expect(screen.queryByRole('columnheader', { name: 'Vrednost' })).not.toBeInTheDocument()
  })

  it('Render_NoReceipts_OffersNewReceipt', async () => {
    // Arrange
    mockApi({ 'GET /api/receipts?': page([]) })

    // Act
    await renderRoutes(routes, '/receipts')

    // Assert
    expect(await screen.findByText('Još nema prijema')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'Novi prijem' })[0]).toHaveAttribute('href', '/receipts/new')
  })
})
