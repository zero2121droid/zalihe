import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LOADER_DELAY_MS, LOADER_MIN_VISIBLE_MS, useLoadingDisplay } from './useLoadingDisplay'

function setup(pending: boolean) {
  return renderHook(({ pending }) => useLoadingDisplay(pending), { initialProps: { pending } })
}

describe('useLoadingDisplay', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('UseLoadingDisplay_NotPending_ShowsContent', () => {
    // Act
    const { result } = setup(false)

    // Assert
    expect(result.current).toBe('content')
  })

  it('UseLoadingDisplay_FastAnswer_NeverShowsLoader', () => {
    // Arrange
    const { result, rerender } = setup(true)
    act(() => vi.advanceTimersByTime(LOADER_DELAY_MS - 50))
    expect(result.current).toBe('blank')

    // Act
    rerender({ pending: false })

    // Assert
    expect(result.current).toBe('content')
  })

  it('UseLoadingDisplay_SlowAnswer_ShowsLoaderAfterDelay', () => {
    // Arrange
    const { result } = setup(true)

    // Act
    act(() => vi.advanceTimersByTime(LOADER_DELAY_MS))

    // Assert
    expect(result.current).toBe('loader')
  })

  it('UseLoadingDisplay_AnswerRightAfterLoaderShows_KeepsLoaderForMinimumTime', () => {
    // Arrange
    const { result, rerender } = setup(true)
    act(() => vi.advanceTimersByTime(LOADER_DELAY_MS + 100))

    // Act
    rerender({ pending: false })

    // Assert
    expect(result.current).toBe('loader')
    act(() => vi.advanceTimersByTime(LOADER_MIN_VISIBLE_MS - 150))
    expect(result.current).toBe('loader')
    act(() => vi.advanceTimersByTime(100))
    expect(result.current).toBe('content')
  })

  it('UseLoadingDisplay_LoaderShownLongEnough_ShowsContentAtOnce', () => {
    // Arrange
    const { result, rerender } = setup(true)
    act(() => vi.advanceTimersByTime(LOADER_DELAY_MS + LOADER_MIN_VISIBLE_MS + 100))

    // Act
    rerender({ pending: false })
    act(() => vi.advanceTimersByTime(0))

    // Assert
    expect(result.current).toBe('content')
  })
})
