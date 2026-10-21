import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../test/render'
import { ResetPasswordPage } from './ResetPasswordPage'

const routes = [
  { path: '/reset-password', element: <ResetPasswordPage /> },
  { path: '/', element: <div data-testid="home" /> },
]

const link = '/reset-password?email=ana%40zrno.rs&token=abc'

describe('ResetPasswordPage', () => {
  it('Submit_ValidLink_SendsEmailTokenAndPasswordThenGoesHome', async () => {
    // Arrange
    const fetchMock = mockApi({ 'POST /api/auth/reset-password': { status: 204 } })
    await renderRoutes(routes, link)

    // Act
    expect(screen.getByText('Upiši novu lozinku za ana@zrno.rs.')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Nova lozinka', { exact: false }), 'nova-sigurna-lozinka')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj i prijavi se' }))

    // Assert
    expect(await screen.findByTestId('home')).toBeInTheDocument()
    const [, init] = fetchMock.mock.calls.find(([url]) => String(url) === '/api/auth/reset-password')!
    expect(JSON.parse(String(init!.body))).toEqual({ email: 'ana@zrno.rs', token: 'abc', newPassword: 'nova-sigurna-lozinka' })
  })

  it('Submit_UsedOrExpiredLink_SaysSoAndOffersANewOne', async () => {
    // Arrange
    mockApi({
      'POST /api/auth/reset-password': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'auth.reset_invalid' }] },
      },
    })
    await renderRoutes(routes, link)

    // Act
    await userEvent.type(screen.getByLabelText('Nova lozinka', { exact: false }), 'nova-sigurna-lozinka')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj i prijavi se' }))

    // Assert
    expect(await screen.findByRole('heading', { name: 'Link ne važi' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Zatraži nov link' })).toHaveAttribute('href', '/forgot-password')
  })

  it('Render_LinkWithoutToken_SaysItIsNotValid', async () => {
    await renderRoutes(routes, '/reset-password?email=ana%40zrno.rs')

    expect(screen.getByRole('heading', { name: 'Link ne važi' })).toBeInTheDocument()
  })

  it('Submit_PasswordTooShort_ShowsItOnThePasswordField', async () => {
    // Arrange
    mockApi({
      'POST /api/auth/reset-password': {
        status: 400,
        body: {
          status: 400,
          code: 'validation.failed',
          errors: [{ code: 'auth.password_too_short', field: 'newPassword', params: { min: 8 } }],
        },
      },
    })
    await renderRoutes(routes, link)

    // Act
    await userEvent.type(screen.getByLabelText('Nova lozinka', { exact: false }), 'kratka')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj i prijavi se' }))

    // Assert
    expect(await screen.findByText('Lozinka mora imati najmanje 8 znakova.')).toBeInTheDocument()
  })
})
