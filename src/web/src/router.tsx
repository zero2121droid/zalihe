import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { PublicOnly, RequireAuth } from './auth/RouteGuards'
import { AppLayout } from './layout/AppLayout'
import { AuthLayout } from './layout/AuthLayout'
import { HomePage } from './pages/HomePage'
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
        children: [
          { path: '/', element: <HomePage /> },
          { path: '/items', element: <ItemsPage /> },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]

export const router = createBrowserRouter(routes)
