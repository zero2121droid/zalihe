import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderPage } from '../../test/render'
import { UsersSection } from './UsersSection'

const me = { id: 'u1', name: 'Miljan', email: 'miljan@zrno.rs', language: 'sr-Latn', tenantId: 't1', tenantName: 'Zrno' }
const page = (items: object[]) => ({ status: 200, body: { items, totalCount: items.length, page: 1, pageSize: 100 } })
const users = page([{ id: 'u1', name: 'Miljan', email: 'miljan@zrno.rs' }])

describe('UsersSection', () => {
  it('Render_UsersAndInvitations_MarksYouAndExpiredInvitations', async () => {
    mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': page([
        { id: 'i1', email: 'ana@zrno.rs', sentAt: '2026-10-19T09:00:00Z', expiresAt: '2026-10-26T09:00:00Z', state: 'pending' },
        { id: 'i2', email: 'marko@zrno.rs', sentAt: '2026-10-01T09:00:00Z', expiresAt: '2026-10-08T09:00:00Z', state: 'expired' },
      ]),
    })

    await renderPage(<UsersSection user={me} />)

    expect(await screen.findByText('(ti)')).toBeInTheDocument()
    expect(await screen.findByText('ana@zrno.rs')).toBeInTheDocument()
    expect(screen.getByText('Poziv je istekao')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Opozovi poziv za marko@zrno.rs' })).toBeInTheDocument()
  })

  it('Invite_NewAddress_SendsItAndSaysWhereItWent', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': page([]),
      'POST /api/account/invitations': {
        status: 200,
        body: { id: 'i1', email: 'ana@zrno.rs', sentAt: '2026-10-20T09:00:00Z', expiresAt: '2026-10-27T09:00:00Z', state: 'pending' },
      },
    })
    await renderPage(<UsersSection user={me} />)

    // Act
    await userEvent.type(screen.getByLabelText('Pozovi novog korisnika'), 'Ana@Zrno.rs')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji poziv' }))

    // Assert
    expect(await screen.findByRole('status')).toHaveTextContent('Poziv je poslat na ana@zrno.rs.')
    const [, init] = fetchMock.mock.calls.find(([url, i]) => String(url) === '/api/account/invitations' && i?.method === 'POST')!
    expect(JSON.parse(String(init!.body))).toEqual({ email: 'Ana@Zrno.rs' })
    expect(screen.getByLabelText('Pozovi novog korisnika')).toHaveValue('')
  })

  it('Invite_AddressWithAnAccount_ShowsItOnTheField', async () => {
    mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': page([]),
      'POST /api/account/invitations': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'invitation.email_taken', field: 'email', params: null }] },
      },
    })
    await renderPage(<UsersSection user={me} />)

    await userEvent.type(screen.getByLabelText('Pozovi novog korisnika'), 'miljan@zrno.rs')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji poziv' }))

    expect(await screen.findByText('Ova adresa već ima nalog u Lagerku.')).toBeInTheDocument()
  })

  it('Revoke_Invitation_DeletesIt', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': page([
        { id: 'i1', email: 'ana@zrno.rs', sentAt: '2026-10-19T09:00:00Z', expiresAt: '2026-10-26T09:00:00Z', state: 'pending' },
      ]),
      'DELETE /api/account/invitations/i1': { status: 204 },
    })
    await renderPage(<UsersSection user={me} />)
    const row = (await screen.findByText('ana@zrno.rs')).closest('div')!.parentElement!

    // Act
    await userEvent.click(within(row).getByRole('button', { name: 'Opozovi poziv za ana@zrno.rs' }))

    // Assert
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([url, i]) => String(url) === '/api/account/invitations/i1' && i?.method === 'DELETE')).toBe(true),
    )
  })
})
