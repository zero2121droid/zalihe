import { QueryClient } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'
import { vi } from 'vitest'
import { AppProviders } from '../AppProviders'
import i18n from '../i18n'

/** Renders routes with the real providers, starting at `path`, in Serbian unless told otherwise. */
export async function renderRoutes(routes: RouteObject[], path: string, language = 'sr-Latn') {
  await i18n.changeLanguage(language)
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  )
  return { router, queryClient }
}

export function renderPage(element: ReactNode, path = '/') {
  return renderRoutes([{ path, element }, { path: '*', element: <div data-testid="navigated" /> }], path)
}

/** Replaces fetch with one answering each call from the given responses, in order. */
export function mockFetch(...responses: Array<{ status: number; body?: unknown }>) {
  const fetchMock = vi.fn<typeof fetch>()
  for (const { status, body } of responses) {
    fetchMock.mockResolvedValueOnce(
      new Response(body === undefined ? null : JSON.stringify(body), {
        status,
        headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
      }),
    )
  }
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

type MockResponse = { status: number; body?: unknown }

/**
 * Replaces fetch with a fake API that answers by URL instead of call order: the first entry whose
 * key is a prefix of "METHOD /path?query" wins, e.g. { 'GET /api/items?': ..., 'POST /api/items': ... }.
 */
export function mockApi(routes: Record<string, MockResponse | ((init?: RequestInit) => MockResponse)>) {
  const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
    const request = `${(init?.method ?? 'GET').toUpperCase()} ${String(input)}`
    const match = Object.entries(routes).find(([key]) => request.startsWith(key))
    if (!match) throw new Error(`No mock for ${request}`)
    const { status, body } = typeof match[1] === 'function' ? match[1](init) : match[1]
    return new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
    })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}
