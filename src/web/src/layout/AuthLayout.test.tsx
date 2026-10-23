import { act, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { renderRoutes } from '../test/render'
import { AuthLayout } from './AuthLayout'

describe('AuthLayout', () => {
  it('Navigate_ScrolledDown_NewPageStartsAtTop', async () => {
    // Arrange
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
    const { router } = await renderRoutes(
      [{ element: <AuthLayout />, children: [{ path: '/register', element: <div /> }, { path: '/login', element: <div /> }] }],
      '/register',
    )
    scrollTo.mockClear()

    // Act
    await act(() => router.navigate('/login'))

    // Assert
    await waitFor(() => expect(scrollTo).toHaveBeenCalledWith(0, 0))
    scrollTo.mockRestore()
  })
})
