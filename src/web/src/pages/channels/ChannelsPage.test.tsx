import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { leaveTo } from '../../lib/navigation'
import { mockApi, renderPage, renderRoutes, testUser } from '../../test/render'
import { ChannelsPage } from './ChannelsPage'

vi.mock('../../lib/navigation', () => ({ leaveTo: vi.fn() }))

const channel = {
  id: 'c1',
  type: 'wooCommerce',
  baseUrl: 'https://zrno.rs',
  status: 'connected',
  lastErrorCode: null,
  failingSince: null,
  lastSyncedAt: null,
  createdAt: '2026-10-02T08:00:00Z',
}

const failed = (code: string, field: string | null = null) => ({
  status: 400,
  body: { status: 400, code: 'validation.failed', errors: [{ code, field }] },
})

describe('ChannelsPage', () => {
  it('Connect_WorkingKeys_SendsKeysAndShowsConnectedShop', async () => {
    // Arrange
    let connected = false
    const fetchMock = mockApi({
      'GET /api/channels': () => ({ status: 200, body: connected ? [channel] : [] }),
      'POST /api/channels/woocommerce': () => {
        connected = true
        return { status: 201, body: channel }
      },
    })
    await renderPage(<ChannelsPage />, '/channels')
    await userEvent.click(await screen.findByRole('button', { name: 'Unesi ključeve ručno' }))

    // Act
    await userEvent.type(await screen.findByLabelText(/^Adresa prodavnice/), 'zrno.rs')
    await userEvent.type(screen.getByLabelText(/^Consumer key/), ' ck_abc123456789 ')
    await userEvent.type(screen.getByLabelText(/^Consumer secret/), 'cs_abc123456789')
    await userEvent.click(screen.getByRole('button', { name: 'Poveži ključevima' }))

    // Assert
    expect(await screen.findByRole('link', { name: 'zrno.rs' })).toHaveAttribute('href', 'https://zrno.rs')
    expect(screen.getByText('Povezano')).toBeInTheDocument()
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(post?.[1]?.body as string)).toEqual({
      baseUrl: 'zrno.rs',
      consumerKey: 'ck_abc123456789',
      consumerSecret: 'cs_abc123456789',
    })
  })

  it('Connect_ShopRejectsKeys_ShowsTranslatedReason', async () => {
    // Arrange
    mockApi({
      'GET /api/channels': { status: 200, body: [] },
      'POST /api/channels/woocommerce': failed('channel.unauthorized'),
    })
    await renderPage(<ChannelsPage />, '/channels')
    await userEvent.click(await screen.findByRole('button', { name: 'Unesi ključeve ručno' }))

    // Act
    await userEvent.type(await screen.findByLabelText(/^Adresa prodavnice/), 'zrno.rs')
    await userEvent.click(screen.getByRole('button', { name: 'Poveži ključevima' }))

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Prodavnica je odbila ključeve')
  })

  it('Approve_InvalidAddress_ShowsErrorOnAddressField', async () => {
    // Arrange
    mockApi({
      'GET /api/channels': { status: 200, body: [] },
      'POST /api/channels/woocommerce/authorizations': failed('channel.url_invalid', 'baseUrl'),
    })
    await renderPage(<ChannelsPage />, '/channels')

    // Act
    await userEvent.type(await screen.findByLabelText(/^Adresa prodavnice/), 'http://zrno.rs')
    await userEvent.click(screen.getByRole('button', { name: 'Poveži prodavnicu' }))

    // Assert
    await waitFor(() =>
      expect(screen.getByLabelText(/^Adresa prodavnice/)).toHaveAccessibleDescription(/Unesi ispravnu adresu prodavnice/),
    )
    expect(leaveTo).not.toHaveBeenCalled()
  })

  it('Approve_ValidAddress_SendsAddressAndGoesToTheShop', async () => {
    // Arrange
    vi.mocked(leaveTo).mockClear()
    const authorizeUrl = 'https://zrno.rs/wc-auth/v1/authorize?app_name=Lagerko'
    const fetchMock = mockApi({
      'GET /api/channels': { status: 200, body: [] },
      'POST /api/channels/woocommerce/authorizations': { status: 200, body: { id: 'a1', authorizeUrl } },
    })
    await renderPage(<ChannelsPage />, '/channels')

    // Act
    await userEvent.type(await screen.findByLabelText(/^Adresa prodavnice/), 'zrno.rs')
    await userEvent.click(screen.getByRole('button', { name: 'Poveži prodavnicu' }))

    // Assert
    await waitFor(() => expect(leaveTo).toHaveBeenCalledWith(authorizeUrl))
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(post?.[1]?.body as string)).toEqual({ baseUrl: 'zrno.rs' })
    expect(screen.queryByLabelText(/^Consumer key/)).not.toBeInTheDocument()
  })

  it('Manual_SwitchingBack_KeepsTheTypedAddress', async () => {
    // Arrange
    mockApi({ 'GET /api/channels': { status: 200, body: [] } })
    await renderPage(<ChannelsPage />, '/channels')
    await userEvent.type(await screen.findByLabelText(/^Adresa prodavnice/), 'zrno.rs')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Unesi ključeve ručno' }))
    const inManual = screen.getByLabelText(/^Adresa prodavnice/)
    await userEvent.click(screen.getByRole('button', { name: 'Poveži bez ključeva' }))

    // Assert
    expect(inManual).toHaveValue('zrno.rs')
    expect(screen.getByLabelText(/^Adresa prodavnice/)).toHaveValue('zrno.rs')
    expect(screen.queryByLabelText(/^Consumer key/)).not.toBeInTheDocument()
  })

  it('Return_Approved_CompletesOnceAndShowsTheShop', async () => {
    // Arrange
    let connected = false
    const fetchMock = mockApi({
      'GET /api/channels/c1/summary': {
        status: 200,
        body: { linkedProducts: 0, ordersWithUnlinked: 0, lastOrderAt: null, lastOrderNumber: null, pendingStock: 0 },
      },
      'GET /api/channels': () => ({ status: 200, body: connected ? [channel] : [] }),
      'POST /api/channels/woocommerce/authorizations/a1/complete': () => {
        connected = true
        return { status: 201, body: channel }
      },
    })

    // Act
    const { router } = await renderRoutes(
      [{ path: '/channels', element: <ChannelsPage /> }],
      '/channels?authorization=a1&success=1&user_id=token',
    )

    // Assert
    expect(await screen.findByRole('link', { name: 'zrno.rs' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
    expect(router.state.location.search).toBe('')
  })

  it('Return_ShopRejectsKeys_ShowsReasonAndTriesAgain', async () => {
    // Arrange
    let attempts = 0
    mockApi({
      'GET /api/channels': { status: 200, body: [] },
      'POST /api/channels/woocommerce/authorizations/a1/complete': () => {
        attempts++
        return failed('channel.unexpected_response')
      },
    })
    await renderRoutes([{ path: '/channels', element: <ChannelsPage /> }], '/channels?authorization=a1&success=1')

    // Act
    expect(await screen.findByRole('alert')).toHaveTextContent('sigurnosni dodatak')
    await userEvent.click(screen.getByRole('button', { name: 'Pokušaj ponovo' }))

    // Assert
    await waitFor(() => expect(attempts).toBe(2))
  })

  it('Return_Denied_SaysSoWithoutCallingTheApiAndStartsOver', async () => {
    // Arrange
    const fetchMock = mockApi({ 'GET /api/channels': { status: 200, body: [] } })
    await renderRoutes([{ path: '/channels', element: <ChannelsPage /> }], '/channels?authorization=a1&success=0')

    // Act
    expect(await screen.findByRole('alert')).toHaveTextContent('Pristup nije odobren')
    await userEvent.click(screen.getByRole('button', { name: 'Počni ispočetka' }))

    // Assert
    expect(await screen.findByRole('button', { name: 'Poveži prodavnicu' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(0)
  })

  it('Check_WhileWaiting_ShowsCheckingThenConnected', async () => {
    // Arrange: the shop answers only when the test lets it.
    let answer!: () => void
    const fetchMock = mockApi({
      'GET /api/channels': { status: 200, body: [channel] },
      'POST /api/channels/c1/check': { status: 200, body: channel },
    })
    const respond = fetchMock.getMockImplementation()!
    fetchMock.mockImplementation(async (input, init) => {
      if (init?.method === 'POST') await new Promise<void>((resolve) => (answer = resolve))
      return respond(input, init)
    })
    await renderPage(<ChannelsPage />, '/channels')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Proveri vezu' }))
    const whileChecking = await screen.findByText('Proveravam vezu…')
    answer()

    // Assert
    expect(whileChecking).toBeInTheDocument()
    expect(await screen.findByText('Povezano')).toBeInTheDocument()
    expect(screen.queryByText('Proveravam vezu…')).not.toBeInTheDocument()
  })

  it('Check_KeysRevoked_ShowsErrorStatusAndReason', async () => {
    // Arrange
    mockApi({
      'GET /api/channels': { status: 200, body: [channel] },
      'POST /api/channels/c1/check': {
        status: 200,
        body: { ...channel, status: 'error', lastErrorCode: 'channel.unauthorized', failingSince: '2026-10-17T12:00:00Z' },
      },
    })
    await renderPage(<ChannelsPage />, '/channels')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Proveri vezu' }))

    // Assert
    expect(await screen.findByText('Veza ne radi')).toBeInTheDocument()
    expect(screen.getByText(/Prodavnica je odbila ključeve/)).toBeInTheDocument()
    expect(screen.getByText(/^Ne radi od 17\. 10\. 2026\./)).toBeInTheDocument()
    expect(screen.getByText(/^Provereno/)).toBeInTheDocument()
  })

  it('ReplaceKeys_NewKeysWork_SendsThemAndClosesModal', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/channels': { status: 200, body: [channel] },
      'PUT /api/channels/c1/credentials': { status: 200, body: channel },
    })
    await renderPage(<ChannelsPage />, '/channels')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Promeni ključeve' }))
    await userEvent.type(await screen.findByLabelText(/^Consumer key/), 'ck_new123456789')
    await userEvent.type(screen.getByLabelText(/^Consumer secret/), 'cs_new123456789')
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj ključeve' }))

    // Assert
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    const put = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')
    expect(JSON.parse(put?.[1]?.body as string)).toEqual({ consumerKey: 'ck_new123456789', consumerSecret: 'cs_new123456789' })
  })

  it('Render_ManagerWithoutShop_SaysTheAdminConnectsIt', async () => {
    // Arrange
    mockApi({
      'GET /api/auth/me': { status: 200, body: { ...testUser, role: 'manager' } },
      'GET /api/channels': { status: 200, body: [] },
    })

    // Act
    await renderPage(<ChannelsPage />, '/channels')

    // Assert
    expect(await screen.findByText(/Prodavnicu povezuje administrator firme/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Poveži prodavnicu' })).not.toBeInTheDocument()
  })
})

