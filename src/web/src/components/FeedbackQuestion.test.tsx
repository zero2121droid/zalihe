import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderPage } from '../test/render'
import { FeedbackQuestion } from './FeedbackQuestion'

const question = <FeedbackQuestion prompt="csvImport" question="Da li je uvoz bio jasan?" />

function answerBody(fetchMock: ReturnType<typeof mockApi>) {
  const call = fetchMock.mock.calls.find(([url]) => String(url) === '/api/feedback/prompts/csvImport')
  return JSON.parse(String(call![1]!.body)) as Record<string, unknown>
}

describe('FeedbackQuestion', () => {
  it('Yes_NotAnsweredBefore_SendsPositiveAndThanks', async () => {
    const fetchMock = mockApi({
      'GET /api/feedback/prompts': { status: 200, body: [] },
      'POST /api/feedback/prompts/csvImport': { status: 204 },
    })
    await renderPage(question, '/items/import')

    await userEvent.click(await screen.findByRole('button', { name: 'Jeste' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Hvala, zabeleženo.')
    expect(answerBody(fetchMock)).toEqual({ positive: true, message: null, page: '/items/import' })
  })

  it('NotReally_WithText_SendsTheText', async () => {
    const fetchMock = mockApi({
      'GET /api/feedback/prompts': { status: 200, body: [] },
      'POST /api/feedback/prompts/csvImport': { status: 204 },
    })
    await renderPage(question, '/items/import')

    await userEvent.click(await screen.findByRole('button', { name: 'Ne baš' }))
    await userEvent.type(screen.getByLabelText('Šta je bilo nejasno? Jedna rečenica je dovoljna.'), 'Kolona za minimum.')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji' }))

    await screen.findByRole('status')
    expect(answerBody(fetchMock)).toMatchObject({ positive: false, message: 'Kolona za minimum.' })
  })

  it('AlreadyAnswered_ShowsNothing', async () => {
    mockApi({ 'GET /api/feedback/prompts': { status: 200, body: ['csvImport'] } })
    await renderPage(<div>{question}<span>gotovo</span></div>, '/items/import')

    await screen.findByText('gotovo')
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.queryByText('Da li je uvoz bio jasan?')).not.toBeInTheDocument()
  })
})
