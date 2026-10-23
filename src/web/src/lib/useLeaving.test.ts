import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LEAVING_MS, useLeaving } from './useLeaving'

const keyOf = (id: string) => id

function render(items: string[], scope = 'all') {
  return renderHook(({ items, scope }) => useLeaving(items, keyOf, scope), { initialProps: { items, scope } })
}

describe('useLeaving', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('FirstList_Shown_MarksNothingLeaving', () => {
    // Arrange & Act
    const { result } = render(['a', 'b'])

    // Assert
    expect(result.current).toEqual([
      { item: 'a', leaving: false },
      { item: 'b', leaving: false },
    ])
  })

  it('Rerender_RowDropsOut_KeepsItAtItsPlaceThenRemovesIt', () => {
    // Arrange
    vi.useFakeTimers()
    const { result, rerender } = render(['a', 'b', 'c'])

    // Act
    rerender({ items: ['a', 'c'], scope: 'all' })

    // Assert
    expect(result.current).toEqual([
      { item: 'a', leaving: false },
      { item: 'b', leaving: true },
      { item: 'c', leaving: false },
    ])
    act(() => vi.advanceTimersByTime(LEAVING_MS))
    expect(result.current.map((s) => s.item)).toEqual(['a', 'c'])
  })

  it('Rerender_OtherScope_ReplacesListAtOnce', () => {
    // Arrange
    const { result, rerender } = render(['a', 'b'])

    // Act
    rerender({ items: ['c'], scope: 'unlinked' })

    // Assert
    expect(result.current).toEqual([{ item: 'c', leaving: false }])
  })

  it('Rerender_StandInList_ShowsItAsIs', () => {
    // Arrange
    const { result, rerender } = renderHook(({ items, final }) => useLeaving(items, keyOf, 'all', final), {
      initialProps: { items: ['a', 'b'], final: true },
    })

    // Act
    rerender({ items: ['b'], final: false })

    // Assert
    expect(result.current).toEqual([{ item: 'b', leaving: false }])
  })
})
