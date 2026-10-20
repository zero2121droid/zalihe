import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { PublicOnly, RequireAuth } from './auth/RouteGuards'
import { AcceptInvitationPage } from './pages/AcceptInvitationPage'
import { AppLayout } from './layout/AppLayout'
import { AuthLayout } from './layout/AuthLayout'
import type { RouteHandle } from './layout/pageWidth'
import { ChannelsPage } from './pages/channels/ChannelsPage'
import { ProductImportPage } from './pages/channels/ProductImportPage'
import { HomePage } from './pages/HomePage'
import { ImportPage } from './pages/items/ImportPage'
import { ItemDetailPage } from './pages/items/ItemDetailPage'
import { ItemsPage } from './pages/items/ItemsPage'
import { LoginPage } from './pages/LoginPage'
import { OrdersPage } from './pages/orders/OrdersPage'
import { RegisterPage } from './pages/RegisterPage'
import { ReportsPage } from './pages/reports/ReportsPage'
import { SettingsPage } from './pages/settings/SettingsPage'

export const routes: RouteObject[] = [
  {
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
    element: <AuthLayout />,
    children: [{ path: '/invite/:token', element: <AcceptInvitationPage /> }],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        // Content width per page: 'wide' for screens with tables and overviews, 'normal' (default) for text and forms.
        children: [
          { path: '/', element: <HomePage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/items', element: <ItemsPage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/items/import', element: <ImportPage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/items/:id', element: <ItemDetailPage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/orders', element: <OrdersPage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/reports', element: <ReportsPage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/channels', element: <ChannelsPage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/channels/:id/import', element: <ProductImportPage />, handle: { width: 'wide' } satisfies RouteHandle },
          { path: '/settings', element: <SettingsPage /> },
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
