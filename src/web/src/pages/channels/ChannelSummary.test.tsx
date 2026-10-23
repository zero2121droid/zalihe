import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockApi, renderPage } from '../../test/render'
import { ChannelSummary } from './ChannelSummary'

const summary = (overrides: object = {}) => ({
  status: 200,
  body: { linkedProducts: 64, ordersWithUnlinked: 3, lastOrderAt: null, lastOrderNumber: null, pendingStock: 2, ...overrides },
})

describe('ChannelSummary', () => {
  it('Render_UnlinkedOrdersAndPendingStock_LinksToOrdersAndSaysWhatWaits', async () => {
    // Arrange
    mockApi({ 'GET /api/channels/c1/summary': summary() })

    // Act
    await renderPage(<ChannelSummary channelId="c1" />)

    // Assert
    const strip = await screen.findByRole('region', { name: 'Pregled prodavnice' })
    expect(within(strip).getByText('64')).toBeInTheDocument()
    expect(within(strip).getByRole('link', { name: 'Pogledaj porudžbine' })).toHaveAttribute('href', '/orders?unlinked=1')
    expect(within(strip).getByText('2 proizvoda čekaju slanje')).toBeInTheDocument()
    expect(within(strip).getByText('Još nema porudžbina')).toBeInTheDocument()
  })

  it('Render_AllLinkedAndSent_HasNoLinkAndNothingWaiting', async () => {
    // Arrange
    mockApi({
      'GET /api/channels/c1/summary': summary({ ordersWithUnlinked: 0, pendingStock: 0, lastOrderAt: new Date().toISOString(), lastOrderNumber: '1048' }),
    })

    // Act
    await renderPage(<ChannelSummary channelId="c1" />)

    // Assert
    const strip = await screen.findByRole('region', { name: 'Pregled prodavnice' })
    expect(within(strip).queryByRole('link')).not.toBeInTheDocument()
    expect(within(strip).getByText('Ništa ne čeka slanje')).toBeInTheDocument()
    expect(within(strip).getByText('#1048')).toBeInTheDocument()
  })
})
