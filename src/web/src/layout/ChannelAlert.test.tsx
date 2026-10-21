import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoutes } from '../test/render'
import { ChannelAlert } from './ChannelAlert'

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

const routes = ['/', '/channels'].map((path) => ({ path, element: <><ChannelAlert /><p>{path}</p></> }))

describe('ChannelAlert', () => {
  it('Render_ShopNotWorking_ShowsWhySinceWhenAndWayToChannels', async () => {
    // Arrange
    mockApi({
      'GET /api/channels': {
        status: 200,
        body: [{ ...channel, status: 'error', lastErrorCode: 'channel.unauthorized', failingSince: '2026-10-17T12:00:00Z' }],
      },
    })

    // Act
    await renderRoutes(routes, '/')

    // Assert
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/Veza sa prodavnicom zrno\.rs ne radi od 17\. 10\. 2026\./)
    expect(alert).toHaveTextContent('stanje iz Lagerka ne stiže u prodavnicu')
    expect(screen.getByRole('link', { name: 'Otvori kanale' })).toHaveAttribute('href', '/channels')
  })

  it('Render_ShopFailingButNotYetError_ShowsNothing', async () => {
    // Arrange: a short outage is recorded, but the channel is still connected.
    mockApi({
      'GET /api/channels': {
        status: 200,
        body: [{ ...channel, lastErrorCode: 'channel.unreachable', failingSince: '2026-10-17T12:00:00Z' }],
      },
    })

    // Act
    await renderRoutes(routes, '/')

    // Assert
    expect(await screen.findByText('/')).toBeInTheDocument()
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('Render_OnChannelsPage_LeavesItToTheCard', async () => {
    // Arrange
    mockApi({ 'GET /api/channels': { status: 200, body: [{ ...channel, status: 'error', lastErrorCode: 'channel.unauthorized' }] } })

    // Act
    await renderRoutes(routes, '/channels')

    // Assert
    expect(await screen.findByText('/channels')).toBeInTheDocument()
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
