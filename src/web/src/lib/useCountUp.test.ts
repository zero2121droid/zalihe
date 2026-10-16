import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { countUpDuration, useCountUp } from './useCountUp'

describe('useCountUp', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('UseCountUp_TargetUnknown_StaysAtZero', () => {
    // Arrange / Act
    const { result } = renderHook(() => useCountUp(null))

    // Assert
    expect(result.current).toEqual({ value: 0, finished: false })
  })

  it('UseCountUp_TargetKnown_CountsWholeNumbersUpToTargetThenFinishes', () => {
    // Arrange
    const { result, rerender } = renderHook(({ target }) => useCountUp(target), {
      initialProps: { target: null as number | null },
    })

    // Act
    rerender({ target: 40 })
    act(() => vi.advanceTimersByTime(countUpDuration(40) / 2))

    // Assert
    expect(Number.isInteger(result.current.value)).toBe(true)
    expect(result.current.value).toBeGreaterThan(0)
    expect(result.current.value).toBeLessThan(40)
    expect(result.current.finished).toBe(false)

    // Act
    act(() => vi.advanceTimersByTime(countUpDuration(40)))

    // Assert
    expect(result.current).toEqual({ value: 40, finished: true })
  })

  it('CountUpDuration_ManyOrFewNumbers_StaysBetweenLimits', () => {
    // Act / Assert
    expect(countUpDuration(1)).toBe(600)
    expect(countUpDuration(50)).toBe(1500)
    expect(countUpDuration(5000)).toBe(3000)
  })
})
