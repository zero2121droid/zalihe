import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage, renderRoutes } from '../test/render'
import { RegisterPage } from './RegisterPage'

describe('RegisterPage', () => {
  it('Submit_ApiReturnsFieldErrors_ShowsThemUnderTheFields', async () => {
    // Arrange
    mockFetch({
      status: 400,
      body: {
        status: 400,
        code: 'validation.failed',
        errors: [
          { code: 'auth.email_taken', field: 'email' },
          { code: 'auth.password_too_short', field: 'password', params: { min: 8 } },
        ],
      },
    })
    await renderPage(<RegisterPage />, '/register')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Registruj firmu' }))

    // Assert
    expect(await screen.findByText('Nalog sa ovom email adresom već postoji.')).toBeInTheDocument()
    expect(screen.getByText('Lozinka mora imati najmanje 8 znakova.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('Submit_EnglishSelected_SendsEnglishAsUserLanguage', async () => {
    // Arrange
    const fetchMock = mockFetch({ status: 204 })
    await renderRoutes([{ path: '/register', element: <RegisterPage /> }, { path: '/', element: <div /> }], '/register', 'en')

    // Act
    await userEvent.type(screen.getByLabelText('Company name'), 'Pržionica Zrno')
    await userEvent.type(screen.getByLabelText('Your name'), 'Miljan')
    await userEvent.type(screen.getByLabelText('Email'), 'miljan@example.com')
    await userEvent.type(screen.getByLabelText(/^Password/), 'sigurna-lozinka')
    await userEvent.click(screen.getByRole('button', { name: 'Register company' }))

    // Assert
    const body = JSON.parse(fetchMock.mock.calls[0][1]?.body as string)
    expect(body).toEqual({
      companyName: 'Pržionica Zrno',
      name: 'Miljan',
      email: 'miljan@example.com',
      password: 'sigurna-lozinka',
      language: 'en',
    })
  })
})
