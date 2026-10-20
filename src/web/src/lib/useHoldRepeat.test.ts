import { act, renderHook } from '@testing-library/react'
import type { MouseEvent, PointerEvent } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useHoldRepeat } from './useHoldRepeat'

const press = { button: 0 } as PointerEvent

describe('useHoldRepeat', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('PointerDown_Held_StepsOnceThenRepeatsUntilReleased', () => {
    // Arrange
    const step = vi.fn()
    const { result } = renderHook(() => useHoldRepeat(step))

    // Act
    act(() => result.current.onPointerDown(press))
    act(() => vi.advanceTimersByTime(449))
    const beforeRepeat = step.mock.calls.length
    act(() => vi.advanceTimersByTime(1 + 110 * 3))
    act(() => result.current.onPointerUp())
    act(() => vi.advanceTimersByTime(1000))

    // Assert
    expect(beforeRepeat).toBe(1)
    expect(step).toHaveBeenCalledTimes(4)
  })

  it('Click_FromKeyboard_StepsOnceAndPointerClickDoesNotStepAgain', () => {
    // Arrange
    const step = vi.fn()
    const { result } = renderHook(() => useHoldRepeat(step))

    // Act
    act(() => result.current.onClick({ detail: 0 } as MouseEvent))
    act(() => result.current.onPointerDown(press))
    act(() => result.current.onPointerUp())
    act(() => result.current.onClick({ detail: 1 } as MouseEvent))

    // Assert
    expect(step).toHaveBeenCalledTimes(2)
  })
})
