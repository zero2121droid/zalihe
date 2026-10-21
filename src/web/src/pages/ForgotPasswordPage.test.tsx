import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../test/render'
import { ForgotPasswordPage } from './ForgotPasswordPage'

const routes = [{ path: '/forgot-password', element: <ForgotPasswordPage /> }]

describe('ForgotPasswordPage', () => {
  it('Submit_AnyAddress_SendsItAndSaysToCheckTheEmail', async () => {
    // Arrange
    const fetchMock = mockApi({ 'POST /api/auth/forgot-password': { status: 204 } })
    await renderRoutes(routes, '/forgot-password')

    // Act
    await userEvent.type(screen.getByLabelText('Email'), 'ana@zrno.rs')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji link' }))

    // Assert
    expect(await screen.findByRole('heading', { name: 'Proveri email' })).toBeInTheDocument()
    expect(screen.getByText(/Ako postoji nalog sa adresom ana@zrno.rs/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Nazad na prijavu' })).toHaveAttribute('href', '/login')
    const [, init] = fetchMock.mock.calls.find(([url]) => String(url) === '/api/auth/forgot-password')!
    expect(JSON.parse(String(init!.body))).toEqual({ email: 'ana@zrno.rs' })
  })

  it('Submit_InvalidAddress_ShowsItOnTheEmailField', async () => {
    // Arrange
    mockApi({
      'POST /api/auth/forgot-password': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'validation.email', field: 'email' }] },
      },
    })
    await renderRoutes(routes, '/forgot-password')

    // Act
    await userEvent.type(screen.getByLabelText('Email'), 'ana')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji link' }))

    // Assert
    expect(await screen.findByText('Unesi ispravnu email adresu.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Proveri email' })).not.toBeInTheDocument()
  })
})
