import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import i18n from '../../i18n'
import { mockApi, renderPage } from '../../test/render'
import { SettingsPage } from './SettingsPage'

const me = {
  id: 'u1',
  name: 'Miljan',
  email: 'miljan@zrno.rs',
  language: 'sr-Latn',
  tenantId: 't1',
  tenantName: 'Zrno',
}

const bodyOf = (fetchMock: ReturnType<typeof mockApi>, url: string) => {
  const call = fetchMock.mock.calls.find(([u]) => String(u) === url)
  return call ? JSON.parse(String(call[1]!.body)) : undefined
}

describe('SettingsPage', () => {
  afterEach(() => i18n.changeLanguage('sr-Latn'))

  it('SaveCompany_NewName_SendsItAndConfirms', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/auth/me': { status: 200, body: me },
      'PUT /api/account/company': { status: 200, body: { ...me, tenantName: 'Zrno d.o.o.' } },
    })
    await renderPage(<SettingsPage />)
    const company = await screen.findByRole('region', { name: 'Firma' })

    // Act
    const name = within(company).getByLabelText('Naziv firme')
    await userEvent.clear(name)
    await userEvent.type(name, 'Zrno d.o.o.')
    await userEvent.click(within(company).getByRole('button', { name: 'Sačuvaj' }))

    // Assert
    expect(await within(company).findByText('Sačuvano')).toBeInTheDocument()
    expect(bodyOf(fetchMock, '/api/account/company')).toEqual({ name: 'Zrno d.o.o.' })
  })

  it('SaveProfile_English_SwitchesTheAppToEnglish', async () => {
    // Arrange
    mockApi({
      'GET /api/auth/me': { status: 200, body: me },
      'PUT /api/account/profile': { status: 200, body: { ...me, language: 'en' } },
    })
    await renderPage(<SettingsPage />)
    const profile = await screen.findByRole('region', { name: 'Moj nalog' })

    // Act
    await userEvent.click(within(profile).getByText('English'))
    await userEvent.click(within(profile).getByRole('button', { name: 'Sačuvaj' }))

    // Assert
    expect(await screen.findByRole('heading', { level: 1, name: 'Settings' })).toBeInTheDocument()
  })

  it('ChangePassword_WrongCurrent_ShowsItOnThatField', async () => {
    // Arrange
    mockApi({
      'GET /api/auth/me': { status: 200, body: me },
      'POST /api/account/password': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'auth.wrong_password', field: 'currentPassword', params: null }] },
      },
    })
    await renderPage(<SettingsPage />)
    const password = await screen.findByRole('region', { name: 'Lozinka' })

    // Act
    await userEvent.type(within(password).getByLabelText('Trenutna lozinka'), 'pogresna')
    await userEvent.type(within(password).getByLabelText('Nova lozinka'), 'nova-sigurna-lozinka')
    await userEvent.click(within(password).getByRole('button', { name: 'Sačuvaj' }))

    // Assert
    expect(await within(password).findByText('Trenutna lozinka nije tačna.')).toBeInTheDocument()
  })

  it('ChangePassword_Succeeds_ClearsBothFields', async () => {
    // Arrange
    const fetchMock = mockApi({
      'GET /api/auth/me': { status: 200, body: me },
      'POST /api/account/password': { status: 204 },
    })
    await renderPage(<SettingsPage />)
    const password = await screen.findByRole('region', { name: 'Lozinka' })
    const current = within(password).getByLabelText('Trenutna lozinka')
    const next = within(password).getByLabelText('Nova lozinka')

    // Act
    await userEvent.type(current, 'sigurna-lozinka')
    await userEvent.type(next, 'nova-sigurna-lozinka')
    await userEvent.click(within(password).getByRole('button', { name: 'Sačuvaj' }))

    // Assert
    await waitFor(() => expect(current).toHaveValue(''))
    expect(next).toHaveValue('')
    expect(bodyOf(fetchMock, '/api/account/password')).toEqual({ currentPassword: 'sigurna-lozinka', newPassword: 'nova-sigurna-lozinka' })
  })
})
