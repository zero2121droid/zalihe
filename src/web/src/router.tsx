import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { PublicOnly, RequireAuth } from './auth/RouteGuards'
import { AppLayout } from './layout/AppLayout'
import { AuthLayout } from './layout/AuthLayout'
import type { RouteHandle } from './layout/pageWidth'
import { StartingUp } from './layout/StartingUp'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'

// Home and signing in are in the main bundle; every other page is its own file, loaded when it's first
// opened (the router keeps the current page on screen meanwhile), so the first load stays small.
export const routes: RouteObject[] = [
  {
    HydrateFallback: StartingUp,
    element: <PublicOnly />,
    children: [
      {
        element: <AuthLayout />,
        children: [
          { path: '/login', element: <LoginPage /> },
          { path: '/register', element: <RegisterPage /> },
        ],
      },
    ],
  },
  // The invitation link works signed in or not: accepting signs in as the new user.
  {
    HydrateFallback: StartingUp,
    element: <AuthLayout />,
    children: [{ path: '/invite/:token', lazy: async () => ({ Component: (await import('./pages/AcceptInvitationPage')).AcceptInvitationPage }) }],
  },
  {
    HydrateFallback: StartingUp,
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        // Content width per page: 'wide' for screens with tables and overviews, 'normal' (default) for text and forms.
        children: [
          { path: '/', element: <HomePage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/items', lazy: async () => ({ Component: (await import('./pages/items/ItemsPage')).ItemsPage }), handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/items/import', lazy: async () => ({ Component: (await import('./pages/items/ImportPage')).ImportPage }), handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/items/:id', lazy: async () => ({ Component: (await import('./pages/items/ItemDetailPage')).ItemDetailPage }), handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/orders', lazy: async () => ({ Component: (await import('./pages/orders/OrdersPage')).OrdersPage }), handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/reports', lazy: async () => ({ Component: (await import('./pages/reports/ReportsPage')).ReportsPage }), handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/channels', lazy: async () => ({ Component: (await import('./pages/channels/ChannelsPage')).ChannelsPage }), handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/channels/:id/import', lazy: async () => ({ Component: (await import('./pages/channels/ProductImportPage')).ProductImportPage }), handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/settings', lazy: async () => ({ Component: (await import('./pages/settings/SettingsPage')).SettingsPage }) },
        ],
      },
    ],
  },
  // Development only: every animation running all the time. Not part of the production build.
  ...(import.meta.env.DEV
    ? [{ path: '/dev/animacije', lazy: async () => ({ Component: (await import('./pages/dev/AnimationsPage')).AnimationsPage }) }]
    : []),
  { path: '*', element: <Navigate to="/" replace /> },
]

export const router = createBrowserRouter(routes)
