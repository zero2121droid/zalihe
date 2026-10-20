import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderPage } from '../../test/render'
import { StockCheckSection } from './StockCheckSection'

describe('StockCheckSection', () => {
  it('Run_EverythingMatches_SaysHowManyWereChecked', async () => {
    mockApi({ 'POST /api/stock/recalculate': { status: 200, body: { checkedCount: 312, corrected: [] } } })
    await renderPage(<StockCheckSection />)

    await userEvent.click(screen.getByRole('button', { name: 'Proveri stanje' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Sve se slaže: provereno 312 artikala.')
  })

  it('Run_StockCorrected_ListsWhatChanged', async () => {
    mockApi({
      'POST /api/stock/recalculate': {
        status: 200,
        body: {
          checkedCount: 2,
          corrected: [{ itemId: 'a', name: 'Kafa Etiopija', sku: 'KF-ETI', unit: 'kom', before: 99, after: 7.5 }],
        },
      },
    })
    await renderPage(<StockCheckSection />)

    await userEvent.click(screen.getByRole('button', { name: 'Proveri stanje' }))

    const status = await screen.findByRole('status')
    expect(status).toHaveTextContent('Ispravljen je 1 artikal.')
    expect(status).toHaveTextContent('99 → 7,5 kom')
  })
})
