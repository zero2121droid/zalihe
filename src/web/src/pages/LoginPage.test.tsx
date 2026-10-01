import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage } from '../test/render'
import { LoginPage } from './LoginPage'

describe('LoginPage', () => {
  it('Submit_WrongPassword_ShowsTranslatedError', async () => {
    // Arrange
    mockFetch({ status: 401, body: { status: 401, code: 'auth.invalid_credentials', errors: [] } })
    await renderPage(<LoginPage />, '/login')

    // Act
    await userEvent.type(screen.getByLabelText('Email'), 'miljan@example.com')
    await userEvent.type(screen.getByLabelText('Lozinka'), 'pogresna')
    await userEvent.click(screen.getByRole('button', { name: 'Prijavi se' }))

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Pogrešan email ili lozinka.')
  })

  it('Submit_ValidCredentials_SendsThemAndLeavesThePage', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 204 })
    await renderPage(<LoginPage />, '/login')

    // Act
    await userEvent.type(screen.getByLabelText('Email'), 'miljan@example.com')
    await userEvent.type(screen.getByLabelText('Lozinka'), 'sigurna-lozinka')
    await userEvent.click(screen.getByRole('button', { name: 'Prijavi se' }))

    // Assert
    expect(await screen.findByTestId('navigated')).toBeInTheDocument()
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/auth/login')
    expect(JSON.parse(init?.body as string)).toEqual({
      email: 'miljan@example.com',
      password: 'sigurna-lozinka',
      rememberMe: false,
    })
  })
})
