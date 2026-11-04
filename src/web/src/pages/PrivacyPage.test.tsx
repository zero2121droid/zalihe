import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../test/render'
import { legal } from './legal'
import { PrivacyPage } from './PrivacyPage'

const routes = [{ path: '/privacy', element: <PrivacyPage /> }]

describe('PrivacyPage', () => {
  it('Render_NotSignedIn_ShowsThePolicyWithItsContact', async () => {
    // Arrange
    mockApi({ 'GET /api/auth/me': { status: 401, body: {} } })

    // Act
    await renderRoutes(routes, '/privacy')

    // Assert
    expect(await screen.findByRole('heading', { name: 'Politika privatnosti' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Šta ne čuvamo' })).toBeInTheDocument()
    expect(screen.getByText(new RegExp(legal.controller))).toBeInTheDocument()
  })
})
