import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderPage } from '../../test/render'
import { ChannelsPage } from './ChannelsPage'

const channel = {
  id: 'c1',
  type: 'wooCommerce',
  baseUrl: 'https://zrno.rs',
  status: 'connected',
  lastErrorCode: null,
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

    // Act
    await userEvent.type(await screen.findByLabelText(/^Adresa prodavnice/), 'zrno.rs')
    await userEvent.type(screen.getByLabelText(/^Consumer key/), ' ck_abc123456789 ')
    await userEvent.type(screen.getByLabelText(/^Consumer secret/), 'cs_abc123456789')
    await userEvent.click(screen.getByRole('button', { name: 'Poveži prodavnicu' }))

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

    // Act
    await userEvent.type(await screen.findByLabelText(/^Adresa prodavnice/), 'zrno.rs')
    await userEvent.click(screen.getByRole('button', { name: 'Poveži prodavnicu' }))

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Prodavnica je odbila ključeve')
  })

  it('Connect_InvalidAddress_ShowsErrorOnAddressField', async () => {
    // Arrange
    mockApi({
      'GET /api/channels': { status: 200, body: [] },
      'POST /api/channels/woocommerce': failed('channel.url_invalid', 'baseUrl'),
    })
    await renderPage(<ChannelsPage />, '/channels')

    // Act
    await userEvent.type(await screen.findByLabelText(/^Adresa prodavnice/), 'http://zrno.rs')
    await userEvent.click(screen.getByRole('button', { name: 'Poveži prodavnicu' }))

    // Assert
    await waitFor(() =>
      expect(screen.getByLabelText(/^Adresa prodavnice/)).toHaveAccessibleDescription(/Unesi ispravnu adresu prodavnice/),
    )
  })

  it('Check_KeysRevoked_ShowsErrorStatusAndReason', async () => {
    // Arrange
    mockApi({
      'GET /api/channels': { status: 200, body: [channel] },
      'POST /api/channels/c1/check': {
        status: 200,
        body: { ...channel, status: 'error', lastErrorCode: 'channel.unauthorized' },
      },
    })
    await renderPage(<ChannelsPage />, '/channels')

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Proveri vezu' }))

    // Assert
    expect(await screen.findByText('Veza ne radi')).toBeInTheDocument()
    expect(screen.getByText(/Prodavnica je odbila ključeve/)).toBeInTheDocument()
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
})
