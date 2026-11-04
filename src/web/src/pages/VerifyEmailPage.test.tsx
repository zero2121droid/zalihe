import { act, fireEvent, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockFetch, renderRoutes } from '../test/render'
import { VerifyEmailPage } from './VerifyEmailPage'

const routes = [
  { path: '/verify-email', element: <VerifyEmailPage /> },
  { path: '/', element: <div data-testid="home" /> },
  { path: '/register', element: <div data-testid="register" /> },
]

const entry = { pathname: '/verify-email', state: { email: 'ana@zrno.rs', token: 'tok' } }

async function renderPage(path: typeof entry | string = entry) {
  return renderRoutes(routes, path as string)
}

async function typeCode(code: string) {
  await userEvent.click(screen.getAllByLabelText('Kod iz emaila')[0])
  await userEvent.keyboard(code)
}

async function passSeconds(seconds: number) {
  for (let second = 0; second < seconds; second++) {
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
    })
  }
}

describe('VerifyEmailPage', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('Open_NoRegistrationInProgress_GoesBackToTheForm', async () => {
    // Act
    await renderPage('/verify-email')

    // Assert
    expect(await screen.findByTestId('register')).toBeInTheDocument()
  })

  it('TypeCode_AllSixDigits_ConfirmsWithTheTokenAndGoesHome', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 204 }, { status: 401 })
    await renderPage()
    expect(screen.getByText(/ana@zrno\.rs/)).toBeInTheDocument()

    // Act
    await typeCode('012345')

    // Assert
    expect(await screen.findByTestId('home')).toBeInTheDocument()
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toBe('/api/auth/register/confirm')
    expect(JSON.parse(String(init!.body))).toEqual({ token: 'tok', code: '012345' })
  })

  it('TypeCode_Wrong_ShowsTheErrorAndEmptiesTheCode', async () => {
    // Arrange
    mockFetch({
      status: 400,
      body: { status: 400, code: 'validation.failed', errors: [{ code: 'auth.code_invalid', field: 'code' }] },
    })
    await renderPage()

    // Act
    await typeCode('111111')

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Kod nije tačan.')
    expect(screen.getAllByLabelText('Kod iz emaila')[0]).toHaveValue('')
  })

  it('Confirm_RegistrationReplaced_ShowsWhyTheScreenNoLongerWorks', async () => {
    // Arrange
    mockFetch({
      status: 400,
      body: { status: 400, code: 'validation.failed', errors: [{ code: 'auth.registration_expired' }] },
    })
    await renderPage()

    // Act
    await typeCode('111111')

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Registracija više ne važi. Počni ponovo.')
  })

  it('Resend_BeforeTheWaitIsOver_IsDisabledAndCountsDown', async () => {
    // Arrange
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'], shouldAdvanceTime: true })
    await renderPage()

    // Act
    await passSeconds(10)

    // Assert
    expect(screen.getByRole('button', { name: 'Pošalji ponovo za 50 s' })).toBeDisabled()
  })

  it('Resend_AfterTheWait_AsksForANewCodeWithTheToken', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 204 })
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'], shouldAdvanceTime: true })
    await renderPage()
    await passSeconds(60)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Pošalji ponovo' }))

    // Assert
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled())
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toBe('/api/auth/register/resend')
    expect(JSON.parse(String(init!.body))).toEqual({ token: 'tok' })
    expect(await screen.findByText('Poslato')).toBeInTheDocument()
  })
})
