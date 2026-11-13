import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderPage } from '../test/render'
import { FeedbackModal } from './FeedbackModal'

const shrunk = new Blob(['shrunk'], { type: 'image/webp' })
const shrink = vi.fn(async (_file: Blob, _maxSide?: number) => shrunk)
vi.mock('../lib/shrinkImage', async (original) => ({
  ...(await original<typeof import('../lib/shrinkImage')>()),
  shrinkImage: (file: Blob, maxSide?: number) => shrink(file, maxSide),
}))

beforeEach(() => {
  shrink.mockClear()
  // jsdom has no object URLs; the preview only needs some address.
  URL.createObjectURL = vi.fn(() => 'blob:preview')
  URL.revokeObjectURL = vi.fn()
})

/** The form the modal sent, as plain fields; the screenshot as the Blob itself. */
function sentForm(fetchMock: ReturnType<typeof mockApi>) {
  const call = fetchMock.mock.calls.find(([url, init]) => String(url) === '/api/feedback' && init?.method === 'POST')
  const form = call![1]!.body as FormData
  return Object.fromEntries(form.entries())
}

describe('FeedbackModal', () => {
  it('Send_ProblemWithText_SendsKindPageAndContactThenThanks', async () => {
    const fetchMock = mockApi({ 'POST /api/feedback': { status: 204 } })
    await renderPage(<FeedbackModal opened onClose={() => {}} />, '/items')

    await userEvent.click(await screen.findByRole('button', { name: 'Nešto ne radi' }))
    await userEvent.type(screen.getByLabelText('Poruka'), '  Kamera se ne upali.  ')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Hvala, poslato')
    expect(sentForm(fetchMock)).toEqual({ kind: 'problem', message: 'Kamera se ne upali.', page: '/items', mayContact: 'true' })
  })

  it('Send_EmptyMessage_ShowsRequiredAndSendsNothing', async () => {
    const fetchMock = mockApi({})
    await renderPage(<FeedbackModal opened onClose={() => {}} />, '/items')

    await userEvent.click(await screen.findByRole('button', { name: 'Pošalji' }))

    expect(await screen.findByText('Obavezno polje.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url) === '/api/feedback')).toBe(false)
  })

  it('Send_ContactUnchecked_SendsMayContactFalse', async () => {
    const fetchMock = mockApi({ 'POST /api/feedback': { status: 204 } })
    await renderPage(<FeedbackModal opened onClose={() => {}} />, '/')

    await userEvent.click(await screen.findByLabelText(/Možeš da me kontaktiraš na miljan@zrno.rs/))
    await userEvent.type(screen.getByLabelText('Poruka'), 'Ideja')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji' }))

    await screen.findByRole('status')
    expect(sentForm(fetchMock)).toMatchObject({ kind: 'suggestion', mayContact: 'false' })
  })

  it('Send_TooMany_ShowsTheReason', async () => {
    mockApi({ 'POST /api/feedback': { status: 429, body: { code: 'feedback.too_many' } } })
    await renderPage(<FeedbackModal opened onClose={() => {}} />, '/')

    await userEvent.type(await screen.findByLabelText('Poruka'), 'Ideja')
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Stiglo je mnogo poruka za kratko vreme.')
  })

  it('Send_PickedScreenshot_ShrinksItAndSendsIt', async () => {
    const fetchMock = mockApi({ 'POST /api/feedback': { status: 204 } })
    await renderPage(<FeedbackModal opened onClose={() => {}} />, '/items')
    const file = new File(['png'], 'ekran.png', { type: 'image/png' })

    await userEvent.type(await screen.findByLabelText('Poruka'), 'Vidi sliku')
    await userEvent.upload(document.querySelector<HTMLInputElement>('input[type="file"]')!, file)
    expect(await screen.findByRole('img', { name: 'Slika ekrana koja se šalje' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji' }))

    await screen.findByRole('status')
    expect(shrink).toHaveBeenCalledWith(file, 1600)
    expect(sentForm(fetchMock).screenshot).toBeInstanceOf(Blob)
  })

  it('Send_ScreenshotRemoved_SendsNoScreenshot', async () => {
    const fetchMock = mockApi({ 'POST /api/feedback': { status: 204 } })
    await renderPage(<FeedbackModal opened onClose={() => {}} />, '/items')

    await userEvent.type(await screen.findByLabelText('Poruka'), 'Bez slike')
    await userEvent.upload(document.querySelector<HTMLInputElement>('input[type="file"]')!, new File(['png'], 'a.png', { type: 'image/png' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Ukloni sliku' }))
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji' }))

    await screen.findByRole('status')
    expect(sentForm(fetchMock)).not.toHaveProperty('screenshot')
  })

  it('Paste_Picture_BecomesTheScreenshot', async () => {
    mockApi({})
    await renderPage(<FeedbackModal opened onClose={() => {}} />, '/items')
    const file = new File(['png'], 'paste.png', { type: 'image/png' })

    await userEvent.click(await screen.findByLabelText('Poruka'))
    await userEvent.paste({ files: [file], items: [], types: ['Files'], getData: () => '' } as unknown as DataTransfer)

    expect(await screen.findByRole('img', { name: 'Slika ekrana koja se šalje' })).toBeInTheDocument()
    expect(shrink).toHaveBeenCalledWith(file, 1600)
  })
})
