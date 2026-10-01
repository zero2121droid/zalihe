import { QueryClient } from '@tanstack/react-query'
import { ApiError } from './api/errors'

/** Retrying can't change a 4xx answer (not found, invalid, unauthorized); network and 5xx errors get two more tries. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false
  return failureCount < 2
}

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { refetchOnWindowFocus: true, retry: shouldRetry },
    },
  })
}
