import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { PublicOnly, RequireAuth } from './auth/RouteGuards'
import { AppLayout } from './layout/AppLayout'
import { AuthLayout } from './layout/AuthLayout'
import type { RouteHandle } from './layout/pageWidth'
import { HomePage } from './pages/HomePage'
import { ImportPage } from './pages/items/ImportPage'
import { ItemDetailPage } from './pages/items/ItemDetailPage'
import { ItemsPage } from './pages/items/ItemsPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'

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
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]

export const router = createBrowserRouter(routes)
