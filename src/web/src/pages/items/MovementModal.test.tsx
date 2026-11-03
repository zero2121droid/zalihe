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

const order = {
  status: 201,
  body: {
    id: 'o1', number: 'R-0001', source: 'manual', channelId: null, shopUrl: null, reference: null, note: null,
    status: 'confirmed', takesStock: true, placedAt: '2026-11-03T12:00:00Z', unlinkedLineCount: 0, lines: [],
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
  it('Submit_SaleWithDecimalComma_CreatesOrderWithOneLine', async () => {
    // Arrange
    const fetchMock = mockApi({ 'POST /api/orders': order })
    let closed = false
    await render('sale', () => (closed = true))

    // Act
    await userEvent.type(screen.getByLabelText(/^Prodata količina/), '1,5')
    await userEvent.type(screen.getByLabelText('Referenca'), ' IG 154 ')
    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))

    // Assert
    await waitFor(() => expect(closed).toBe(true))
    expect(postedBody(fetchMock)).toEqual({ lines: [{ itemId: 'item-1', quantity: 1.5 }], reference: 'IG 154', note: null })
  })

  it('Submit_Receipt_RecordsMovementWithoutReference', async () => {
    // Arrange
    const fetchMock = mockApi({ 'POST /api/items/item-1/movements': recorded })
    let closed = false
    await render('receipt', () => (closed = true))

    // Act
    await userEvent.type(screen.getByLabelText(/^Primljena količina/), '24')
    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))

    // Assert
    await waitFor(() => expect(closed).toBe(true))
    expect(screen.queryByLabelText('Referenca')).toBeNull()
    expect(postedBody(fetchMock)).toEqual({ kind: 'receipt', quantity: 24, note: null })
  })

  it('Submit_SaleQuantityRejected_ShowsErrorAtQuantity', async () => {
    // Arrange
    mockApi({
      'POST /api/orders': {
        status: 400,
        body: { code: 'validation.failed', errors: [{ code: 'validation.too_many_decimals', field: 'lines[0].quantity', params: { max: 3 } }] },
      },
    })
    await render('sale')

    // Act
    await userEvent.type(screen.getByLabelText(/^Prodata količina/), '1,2345')
    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))

    // Assert
    expect(await screen.findByText('Najviše 3 decimale.')).toBeInTheDocument()
  })

  it('Submit_Saved_ShowsSavedOnButtonBeforeClosing', async () => {
    // Arrange
    mockApi({ 'POST /api/orders': order })
    // Noted when the modal closes, so a busy machine can't make the test look too late.
    let savedShownOnClose: boolean | null = null
    await render('sale', () => (savedShownOnClose = screen.queryByRole('button', { name: 'Sačuvano' }) !== null))

    // Act
    await userEvent.type(screen.getByLabelText(/^Prodata količina/), '2')
    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))

    // Assert
    await waitFor(() => expect(savedShownOnClose).toBe(true))
  })

  it('Submit_QuantityMissing_ShakesFieldOnEverySubmit', async () => {
    // Arrange
    mockApi({})
    await render('sale')
    const form = screen.getByRole('button', { name: 'Zabeleži' }).closest('form')!

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))
    const first = form.className

    await userEvent.click(screen.getByRole('button', { name: 'Zabeleži' }))

    // Assert: the class switches on each submit, which restarts the shake
    expect(first).not.toBe('')
    expect(form.className).not.toBe('')
    expect(form.className).not.toBe(first)
  })

  it('Type_SaleBeyondStock_WarnsButAllows', async () => {
    // Arrange
    mockApi({})
    await render('sale')

    // Act
    await userEvent.type(screen.getByLabelText(/^Prodata količina/), '6')

    // Assert
    expect(screen.getByText('−2').closest('p')).toHaveTextContent(/ kom$/)
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
    expect(screen.getByText('+3').closest('p')).toHaveTextContent(/ kom$/)
  })

  it('StepButtons_Pressed_ChangeQuantityByOneButNotBelowZero', async () => {
    // Arrange
    mockApi({})
    await render('receipt')
    const field = screen.getByLabelText(/^Primljena količina/)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Povećaj za 1' }))
    await userEvent.click(screen.getByRole('button', { name: 'Povećaj za 1' }))
    const afterTwoUp = (field as HTMLInputElement).value
    await userEvent.clear(field)
    await userEvent.type(field, '0,5')
    await userEvent.click(screen.getByRole('button', { name: 'Smanji za 1' }))

    // Assert
    expect(afterTwoUp).toBe('2')
    expect(field).toHaveValue('0')
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
