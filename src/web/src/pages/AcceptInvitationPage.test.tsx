import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../test/render'
import { AcceptInvitationPage } from './AcceptInvitationPage'

const routes = [
  { path: '/invite/:token', element: <AcceptInvitationPage /> },
  { path: '/', element: <div data-testid="home" /> },
  { path: '/login', element: <div data-testid="login" /> },
]

const info = { companyName: 'Zrno', email: 'ana@zrno.rs', invitedBy: 'Miljan' }

describe('AcceptInvitationPage', () => {
  it('Accept_ValidLink_SendsNamePasswordAndLanguageThenGoesHome', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/invitations/abc': { status: 200, body: info },
      'POST /api/invitations/abc/accept': { status: 204 },
    })
    await renderRoutes(routes, '/invite/abc')

    // Act
    expect(await screen.findByRole('heading', { name: 'Poziv u firmu Zrno' })).toBeInTheDocument()
    expect(screen.getByText('ana@zrno.rs')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Tvoje ime'), 'Ana')
    await userEvent.type(screen.getByLabelText('Lozinka', { exact: false }), 'sigurna-lozinka')
    await userEvent.click(screen.getByRole('button', { name: 'Prihvati poziv' }))

    // Assert
    expect(await screen.findByTestId('home')).toBeInTheDocument()
    const [, init] = fetchMock.mock.calls.find(([url]) => String(url) === '/api/invitations/abc/accept')!
    expect(JSON.parse(String(init!.body))).toEqual({ name: 'Ana', password: 'sigurna-lozinka', language: 'sr-Latn' })
  })

  it('Render_UsedOrExpiredLink_SaysSoAndOffersSignIn', async () => {
    mockApi({ 'GET /api/invitations/staro': { status: 404, body: { status: 404, code: 'invitation.invalid' } } })

    await renderRoutes(routes, '/invite/staro')

    expect(await screen.findByRole('heading', { name: 'Poziv ne važi' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Na prijavu' })).toHaveAttribute('href', '/login')
  })

  it('Accept_PasswordTooShort_ShowsItOnThePasswordField', async () => {
    // Arrange
    mockApi({
      'GET /api/invitations/abc': { status: 200, body: info },
      'POST /api/invitations/abc/accept': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'auth.password_too_short', field: 'password', params: { min: 8 } }] },
      },
    })
    await renderRoutes(routes, '/invite/abc')
    await screen.findByRole('heading', { name: 'Poziv u firmu Zrno' })

    // Act
    await userEvent.type(screen.getByLabelText('Tvoje ime'), 'Ana')
    await userEvent.type(screen.getByLabelText('Lozinka', { exact: false }), 'kratka')
    await userEvent.click(screen.getByRole('button', { name: 'Prihvati poziv' }))

    // Assert
    await waitFor(() => expect(screen.getByText('Lozinka mora imati najmanje 8 znakova.')).toBeInTheDocument())
  })
})
