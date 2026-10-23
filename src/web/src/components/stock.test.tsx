import { act, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AppProviders } from '../AppProviders'
import i18n from '../i18n'
import { StockQuantity, StockStatusLabel } from './stock'

function quantity(value: number, animate = true) {
  return (
    <AppProviders>
      <div data-testid="quantity">
        <StockQuantity quantity={value} unit="kom" status="inStock" animate={animate} />
      </div>
    </AppProviders>
  )
}

function reduceMotion() {
  vi.spyOn(window, 'matchMedia').mockImplementation(
    (query: string) =>
      ({
        matches: query.includes('prefers-reduced-motion'),
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
        addListener: () => {},
        removeListener: () => {},
      }) as unknown as MediaQueryList,
  )
}

describe('StockQuantity', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('sr-Latn')
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('Rerender_QuantityIncreases_ShowsChangeAndEndsOnNewValue', () => {
    // Arrange
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] })
    const { rerender } = render(quantity(31))

    // Act
    rerender(quantity(1255))
    act(() => vi.advanceTimersByTime(1000))

    // Assert
    expect(screen.getByText('+1.224')).toBeInTheDocument()
    expect(screen.getByTestId('quantity')).toHaveTextContent(/1\.255 kom$/)
  })

  it('Rerender_QuantityDecreases_ShowsChangeWithTrueMinus', () => {
    // Arrange
    const { rerender } = render(quantity(31))

    // Act
    rerender(quantity(29))

    // Assert
    expect(screen.getByText('−2')).toBeInTheDocument()
  })

  it('Render_FirstTime_ShowsNoChange', () => {
    // Act
    render(quantity(31))

    // Assert
    expect(screen.getByTestId('quantity')).toHaveTextContent(/^31 kom$/)
  })

  it('Rerender_ReducedMotion_ShowsNewValueAtOnceWithoutChange', () => {
    // Arrange
    reduceMotion()
    const { rerender } = render(quantity(31))

    // Act
    rerender(quantity(55))

    // Assert
    expect(screen.getByTestId('quantity')).toHaveTextContent(/^55 kom$/)
  })

  it('Rerender_NotAnimated_ShowsNewValueAtOnceWithoutChange', () => {
    // Arrange
    const { rerender } = render(quantity(31, false))

    // Act
    rerender(quantity(55, false))

    // Assert
    expect(screen.getByTestId('quantity')).toHaveTextContent(/^55 kom$/)
  })
})

describe('StockStatusLabel', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('sr-Latn')
  })

  it('Rerender_StockFallsBelowMinimum_ShowsNewStatus', () => {
    // Arrange
    const label = (status: 'inStock' | 'low') => (
      <AppProviders>
        <StockStatusLabel status={status} animate />
      </AppProviders>
    )
    const { rerender } = render(label('inStock'))

    // Act
    rerender(label('low'))

    // Assert
    expect(screen.getByText('Ispod minimuma')).toBeInTheDocument()
    expect(screen.queryByText('Na stanju')).not.toBeInTheDocument()
  })
})
