import { ApiError } from './errors'

const XSRF_COOKIE = 'XSRF-TOKEN'
const XSRF_HEADER = 'X-XSRF-TOKEN'
const SAFE_METHODS = ['GET', 'HEAD', 'OPTIONS']

function readCookie(name: string): string | null {
  const prefix = `${name}=`
  const cookie = document.cookie.split('; ').find((c) => c.startsWith(prefix))
  return cookie ? decodeURIComponent(cookie.slice(prefix.length)) : null
}

/**
 * Fetch used by the generated API client (see orval.config.ts).
 * Sends the auth cookie, adds the antiforgery header to state-changing requests,
 * and throws ApiError for non-2xx responses.
 */
export async function customFetch<T>(url: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  const method = (options.method ?? 'GET').toUpperCase()
  if (!SAFE_METHODS.includes(method)) {
    const token = readCookie(XSRF_COOKIE)
    if (token) headers.set(XSRF_HEADER, token)
  }

  const response = await fetch(url, { ...options, headers, credentials: 'same-origin' })
  if (!response.ok) {
    throw await ApiError.fromResponse(response)
  }

  if (response.status === 204) {
    return undefined as T
  }
  const contentType = response.headers.get('content-type') ?? ''
  return (contentType.includes('json') ? await response.json() : await response.text()) as T
}

export type ErrorType<E> = ApiError & { body?: E }
export type BodyType<B> = B
