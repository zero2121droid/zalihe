import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../../test/render'
import { PlatformPage } from './PlatformPage'

const routes = [{ path: '/platform', element: <PlatformPage /> }]

const company = (overrides: object = {}) => ({
  id: 'c1',
  name: 'Pržionica Sever',
  registeredAt: '2026-11-02T10:00:00Z',
  activeItems: 143,
  hasShop: true,
  lastMovementAt: '2026-11-03T12:00:00Z',
  isOwn: false,
  ...overrides,
})

const summary = { status: 200, body: { companies: 12, newLast7Days: 3, activeLast7Days: 5, withShop: 4 } }
const page = (items: object[]) => ({ status: 200, body: { items, totalCount: items.length, page: 1, pageSize: 20 } })

describe('PlatformPage', () => {
  it('Render_Summary_ShowsTheTotals', async () => {
    // Arrange
    mockApi({ 'GET /api/platform/summary': summary, 'GET /api/platform/companies': page([company()]) })

    // Act
    await renderRoutes(routes, '/platform')

    // Assert
    const strip = within(await screen.findByRole('region', { name: 'Pregled svih firmi' }))
    expect(await strip.findByText('12')).toBeInTheDocument()
    expect(strip.getByText('3')).toBeInTheDocument()
    expect(strip.getByText('5')).toBeInTheDocument()
    expect(strip.getByText('4')).toBeInTheDocument()
  })

  it('Render_Companies_ShowsCountsShopAndMarksTheOwnersCompany', async () => {
    // Arrange
    mockApi({
      'GET /api/platform/summary': summary,
      'GET /api/platform/companies': page([
        company(),
        company({ id: 'c2', name: 'Zrno', activeItems: 0, hasShop: false, lastMovementAt: null, isOwn: true }),
      ]),
    })

    // Act
    await renderRoutes(routes, '/platform')

    // Assert
    const active = within((await screen.findByText('Pržionica Sever')).closest('tr')!)
    expect(active.getByText('143')).toBeInTheDocument()
    expect(active.getByText('povezana')).toBeInTheDocument()
    expect(active.queryByText('tvoja')).not.toBeInTheDocument()

    const own = within(screen.getByText('Zrno').closest('tr')!)
    expect(own.getByText('tvoja')).toBeInTheDocument()
    expect(own.getByText('nema')).toBeInTheDocument()
    expect(own.getByText('još nema')).toBeInTheDocument()
  })

  it('Render_NoCompanies_ExplainsTheEmptyList', async () => {
    // Arrange
    mockApi({ 'GET /api/platform/summary': summary, 'GET /api/platform/companies': page([]) })

    // Act
    await renderRoutes(routes, '/platform')

    // Assert
    expect(await screen.findByText('Još nema registrovanih firmi.')).toBeInTheDocument()
  })

  it('Render_RequestFails_ShowsTheError', async () => {
    // Arrange
    mockApi({
      'GET /api/platform/summary': summary,
      'GET /api/platform/companies': { status: 403, body: { code: 'auth.forbidden' } },
    })

    // Act
    await renderRoutes(routes, '/platform')

    // Assert
    expect(await screen.findByRole('alert')).toBeInTheDocument()
  })
})
