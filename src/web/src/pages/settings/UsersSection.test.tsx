import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderPage, testUser } from '../../test/render'
import { UsersSection } from './UsersSection'

const me = testUser
const page = (items: object[]) => ({ status: 200, body: { items, totalCount: items.length, page: 1, pageSize: 100 } })
/** Mantine labels a select's list too; the field is the input. */
const selectInput = (label: string) =>
  screen.getAllByLabelText(label).find((element): element is HTMLInputElement => element instanceof HTMLInputElement)!

/** Opens the select labelled `label` and picks `option` from its own list (every role select has the same options). */
async function choose(label: string, option: string) {
  const input = selectInput(label)
  await userEvent.click(input)
  const list = document.getElementById(input.getAttribute('aria-controls')!)!
  // jsdom never finishes the dropdown's open transition, so the options count as hidden.
  await userEvent.click(within(list).getByRole('option', { name: option, hidden: true }))
}

const users = page([{ id: 'u1', name: 'Miljan', email: 'miljan@zrno.rs', role: 'admin' }])

describe('UsersSection', () => {
  it('Render_UsersAndInvitations_MarksYouAndExpiredInvitations', async () => {
    mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': page([
        { id: 'i1', email: 'ana@zrno.rs', sentAt: '2026-10-19T09:00:00Z', expiresAt: '2026-10-26T09:00:00Z', state: 'pending', role: 'employee' },
        { id: 'i2', email: 'marko@zrno.rs', sentAt: '2026-10-01T09:00:00Z', expiresAt: '2026-10-08T09:00:00Z', state: 'expired', role: 'manager' },
      ]),
    })

    await renderPage(<UsersSection user={me} />)

    expect(await screen.findByText('(ti)')).toBeInTheDocument()
    expect(await screen.findByText('ana@zrno.rs')).toBeInTheDocument()
    expect(screen.getByText(/Menadžer · Poziv je istekao/)).toBeInTheDocument()
    expect(screen.getByText(/Zaposleni · Poziv poslat/)).toBeInTheDocument()
    expect(selectInput('Uloga za Miljan')).toHaveValue('Administrator')
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
    expect(JSON.parse(String(init!.body))).toEqual({ email: 'Ana@Zrno.rs', role: 'employee' })
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

  it('Resend_Invitation_SaysSentThenOffersItAgain', async () => {
    // Arrange
    const invitation = { id: 'i1', email: 'ana@zrno.rs', sentAt: '2026-10-19T09:00:00Z', expiresAt: '2026-10-26T09:00:00Z', state: 'pending' }
    mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': page([invitation]),
      'POST /api/account/invitations': { status: 200, body: invitation },
    })
    await renderPage(<UsersSection user={me} />)

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Pošalji ponovo' }))

    // Assert
    expect(await screen.findByRole('button', { name: 'Poslato' })).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Pošalji ponovo' }, { timeout: 3000 })).toBeInTheDocument()
  })

  it('Revoke_Invitation_RemovesItsRow', async () => {
    // Arrange
    let revoked = false
    mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': () =>
        page(revoked ? [] : [{ id: 'i1', email: 'ana@zrno.rs', sentAt: '2026-10-19T09:00:00Z', expiresAt: '2026-10-26T09:00:00Z', state: 'pending' }]),
      'DELETE /api/account/invitations/i1': () => {
        revoked = true
        return { status: 204 }
      },
    })
    await renderPage(<UsersSection user={me} />)

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Opozovi poziv za ana@zrno.rs' }))

    // Assert
    await waitFor(() => expect(screen.queryByText('ana@zrno.rs')).not.toBeInTheDocument())
  })

  it('ChangeRole_LastAdmin_ShowsWhyNotAndKeepsTheRole', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': page([]),
      'PUT /api/account/users/u1/role': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'user.last_admin', field: 'role' }] },
      },
    })
    await renderPage(<UsersSection user={me} />)

    // Act
    await screen.findAllByLabelText('Uloga za Miljan')
    await choose('Uloga za Miljan', 'Menadžer')

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Firma mora imati bar jednog administratora')
    const put = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')
    expect(JSON.parse(String(put![1]!.body))).toEqual({ role: 'manager' })
    expect(selectInput('Uloga za Miljan')).toHaveValue('Administrator')
  })

  it('Invite_ManagerChosen_SendsTheRoleAndExplainsIt', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/account/users': users,
      'GET /api/account/invitations': page([]),
      'POST /api/account/invitations': {
        status: 200,
        body: { id: 'i1', email: 'ana@zrno.rs', sentAt: '2026-10-20T09:00:00Z', expiresAt: '2026-10-27T09:00:00Z', state: 'pending', role: 'manager' },
      },
    })
    await renderPage(<UsersSection user={me} />)

    // Act
    await userEvent.type(await screen.findByLabelText('Pozovi novog korisnika'), 'ana@zrno.rs')
    await choose('Uloga', 'Menadžer')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji poziv' }))

    // Assert
    expect(screen.getByText(/izveštaje i nabavne cene/)).toBeInTheDocument()
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true))
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post![1]!.body))).toEqual({ email: 'ana@zrno.rs', role: 'manager' })
  })
})
