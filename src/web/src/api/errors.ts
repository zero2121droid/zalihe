import type { TFunction } from 'i18next'

export interface ApiErrorItem {
  code: string
  field?: string | null
  params?: Record<string, unknown> | null
}

const VALIDATION_FAILED = 'validation.failed'
const UNEXPECTED = 'common.unexpected'

/**
 * An error from the API, parsed from its ProblemDetails body.
 * The API sends codes (e.g. "auth.email_taken"); they are translated via "errors.*" keys.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly errors: ApiErrorItem[]

  constructor(status: number, code: string, errors: ApiErrorItem[] = []) {
    super(code)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.errors = errors
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    try {
      const body = (await response.json()) as { code?: string; errors?: ApiErrorItem[] }
      return new ApiError(response.status, body.code ?? UNEXPECTED, body.errors ?? [])
    } catch {
      return new ApiError(response.status, UNEXPECTED)
    }
  }
}

function translateCode(t: TFunction, code: string, params?: Record<string, unknown> | null): string {
  return t(`errors.${code}`, { ...params, defaultValue: t(`errors.${UNEXPECTED}`) })
}

/** Message for the form as a whole: errors not tied to a field, or the error code itself. */
export function formErrorMessage(t: TFunction, error: unknown): string | null {
  if (!error) return null
  if (!(error instanceof ApiError)) return translateCode(t, 'common.network')

  if (error.code === VALIDATION_FAILED) {
    const general = error.errors.find((e) => !e.field)
    return general ? translateCode(t, general.code, general.params) : null
  }
  return translateCode(t, error.code)
}

/** Translated messages per form field, keyed by the API field name (e.g. "email"). */
export function fieldErrorMessages(t: TFunction, error: unknown): Record<string, string> {
  if (!(error instanceof ApiError)) return {}

  const messages: Record<string, string> = {}
  for (const item of error.errors) {
    if (item.field && !messages[item.field]) {
      messages[item.field] = translateCode(t, item.code, item.params)
    }
  }
  return messages
}
