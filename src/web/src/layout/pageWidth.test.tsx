import { screen } from '@testing-library/react'
import { Outlet } from 'react-router'
import { describe, expect, it } from 'vitest'
import { renderRoutes } from '../test/render'
import { type RouteHandle, usePageWidth } from './pageWidth'

function ShowWidth() {
  return (
    <div data-testid="width">
      {usePageWidth()}
      <Outlet />
    </div>
  )
}

const routes = [
  {
    element: <ShowWidth />,
    children: [
      { path: '/', element: null },
      { path: '/items', element: null, handle: { width: 'wide' } satisfies RouteHandle },
    ],
  },
]

describe('usePageWidth', () => {
  it('usePageWidth_RouteMarkedWide_ReturnsWide', async () => {
    await renderRoutes(routes, '/items')
    expect(screen.getByTestId('width')).toHaveTextContent('wide')
  })

  it('usePageWidth_RouteWithoutHandle_ReturnsNormal', async () => {
    await renderRoutes(routes, '/')
    expect(screen.getByTestId('width')).toHaveTextContent('normal')
  })
})
