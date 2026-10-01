import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderRoutes } from '../test/render'
import { PublicOnly, RequireAuth } from './RouteGuards'

const routes = [
  { element: <RequireAuth />, children: [{ path: '/', element: <div>app</div> }] },
  { element: <PublicOnly />, children: [{ path: '/login', element: <div>login</div> }] },
]

describe('RequireAuth', () => {
  it('Render_AnonymousUser_RedirectsToLogin', async () => {
    // Arrange
    mockFetch({ status: 401, body: { status: 401, code: 'auth.unauthenticated' } })

    // Act
    const { router } = await renderRoutes(routes, '/')

    // Assert
    expect(await screen.findByText('login')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('Render_SignedInUser_ShowsTheApp', async () => {
    // Arrange
    mockFetch({
      status: 200,
      body: { id: '1', name: 'Miljan', email: 'm@example.com', language: 'sr-Latn', tenantId: '2', tenantName: 'Zrno' },
    })

    // Act
    await renderRoutes(routes, '/')

    // Assert
    expect(await screen.findByText('app')).toBeInTheDocument()
  })
})
