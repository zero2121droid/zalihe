import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { ItemDto, ManualMovementKind } from '../../api/generated/model'
import { mockApi, renderPage } from '../../test/render'
import { MovementModal } from './MovementModal'

const item = {
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
} as ItemDto

const recorded = {
  status: 201,
  body: {
    movement: { id: 'm1', type: 'sale', quantity: -2, occurredAt: '2026-10-01T12:00:00Z', source: 'manual', externalRef: null, note: null, userName: 'Miljan' },
    stock: 2,
  },
}

function render(kind: ManualMovementKind, onClose = () => {}) {
  return renderPage(<MovementModal item={item} kind={kind} onClose={onClose} />)
}

function postedBody(fetchMock: ReturnType<typeof mockApi>) {
  const call = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
  return JSON.parse(call?.[1]?.body as string)
}

describe('MovementModal', () => {
  it('Submit_SaleWithDecimalComma_SendsKindAndQuantity', async () => {
    // Arrange
    const fetchMock = mockApi({ 'POST /api/items/item-1/movements': recorded })
    let closed = false
    await render('sale', () => (closed = true))

    // Act
    await userEvent.type(screen.getByLabelText(/^Prodata količina/), '1,5')
    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))

    // Assert
    await waitFor(() => expect(closed).toBe(true))
    expect(postedBody(fetchMock)).toEqual({ kind: 'sale', quantity: 1.5, note: null })
  })

  it('Type_SaleBeyondStock_WarnsButAllows', async () => {
    // Arrange
    mockApi({})
    await render('sale')

    // Act
    await userEvent.type(screen.getByLabelText(/^Prodata količina/), '6')

    // Assert
    expect(screen.getByText('−2 kom')).toBeInTheDocument()
    expect(screen.getByText(/Stanje će otići u minus/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Zabeleži' })).toBeEnabled()
  })

  it('Type_Count_ShowsDifferenceFromCurrentStock', async () => {
    // Arrange
    mockApi({})
    await render('count')

    // Act
    await userEvent.type(screen.getByLabelText(/^Prebrojano stanje/), '7')

    // Assert
    expect(screen.getByText('Razlika')).toBeInTheDocument()
    expect(screen.getByText('+3 kom')).toBeInTheDocument()
  })

  it('Submit_CountWithoutReason_ShowsApiErrorUnderNote', async () => {
    // Arrange
    mockApi({
      'POST /api/items/item-1/movements': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'validation.required', field: 'note' }] },
      },
    })
    await render('count')

    // Act
    await userEvent.type(screen.getByLabelText(/^Prebrojano stanje/), '7')
    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))

    // Assert
    expect(await screen.findByText('Obavezno polje.')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Razlog/)).toHaveAttribute('aria-invalid', 'true')
  })

  it('Submit_EmptyQuantity_ShowsRequiredWithoutCallingApi', async () => {
    // Arrange
    const fetchMock = mockApi({})
    await render('receipt')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))

    // Assert
    expect(await screen.findByText('Obavezno polje.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
