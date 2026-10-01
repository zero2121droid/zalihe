import { beforeAll, describe, expect, it } from 'vitest'
import i18n from '../i18n'
import { ApiError, fieldErrorMessages, formErrorMessage } from './errors'

const t = i18n.t.bind(i18n)

beforeAll(async () => {
  await i18n.changeLanguage('sr-Latn')
})

describe('formErrorMessage', () => {
  it('formErrorMessage_KnownCode_ReturnsTranslation', () => {
    // Arrange
    const error = new ApiError(401, 'auth.invalid_credentials')

    // Act & Assert
    expect(formErrorMessage(t, error)).toBe('Pogrešan email ili lozinka.')
  })

  it('formErrorMessage_UnknownCode_ReturnsGenericMessage', () => {
    expect(formErrorMessage(t, new ApiError(400, 'something.new'))).toBe(
      'Došlo je do neočekivane greške. Pokušaj ponovo.',
    )
  })

  it('formErrorMessage_ValidationWithOnlyFieldErrors_ReturnsNull', () => {
    // Arrange
    const error = new ApiError(400, 'validation.failed', [{ code: 'validation.required', field: 'email' }])

    // Act & Assert
    expect(formErrorMessage(t, error)).toBeNull()
  })

  it('formErrorMessage_NotAnApiError_ReturnsNetworkMessage', () => {
    expect(formErrorMessage(t, new TypeError('Failed to fetch'))).toBe(
      'Server nije dostupan. Proveri internet vezu i pokušaj ponovo.',
    )
  })
})

describe('fieldErrorMessages', () => {
  it('fieldErrorMessages_ErrorWithParams_InterpolatesThem', () => {
    // Arrange
    const error = new ApiError(400, 'validation.failed', [
      { code: 'auth.password_too_short', field: 'password', params: { min: 8 } },
      { code: 'auth.email_taken', field: 'email' },
    ])

    // Act
    const messages = fieldErrorMessages(t, error)

    // Assert
    expect(messages).toEqual({
      password: 'Lozinka mora imati najmanje 8 znakova.',
      email: 'Nalog sa ovom email adresom već postoji.',
    })
  })
})
