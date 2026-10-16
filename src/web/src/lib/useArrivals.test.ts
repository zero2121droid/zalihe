import { renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { useArrivals } from './useArrivals'

function setup(ids: string[] | undefined, scope = 'all:1') {
  return renderHook(({ ids, scope }) => useArrivals(ids, scope), { initialProps: { ids, scope } })
}

describe('useArrivals', () => {
  it('UseArrivals_FirstLoad_MarksNothing', () => {
    // Act
    const { result } = setup(['b', 'a'])

    // Assert
    expect([...result.current]).toEqual([])
  })

  it('UseArrivals_NewIdOnTop_MarksOnlyIt', () => {
    // Arrange
    const { result, rerender } = setup(['b', 'a'])

    // Act
    rerender({ ids: ['c', 'b', 'a'], scope: 'all:1' })

    // Assert
    expect([...result.current]).toEqual(['c'])
  })

  it('UseArrivals_OtherScope_MarksNothing', () => {
    // Arrange
    const { result, rerender } = setup(['b', 'a'])

    // Act
    rerender({ ids: ['x', 'y'], scope: 'all:2' })

    // Assert
    expect([...result.current]).toEqual([])
  })

  it('UseArrivals_ListNotFinal_KeepsWhatItKnew', () => {
    // Arrange
    const { result, rerender } = setup(['b', 'a'])

    // Act
    rerender({ ids: undefined, scope: 'all:1' })
    rerender({ ids: ['c', 'b', 'a'], scope: 'all:1' })

    // Assert
    expect([...result.current]).toEqual(['c'])
  })
})
