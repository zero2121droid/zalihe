import { describe, expect, it } from 'vitest'
import { ApiError } from './api/errors'
import { shouldRetry } from './queryClient'

describe('shouldRetry', () => {
  it('shouldRetry_NotFound_DoesNotRetry', () => {
    expect(shouldRetry(0, new ApiError(404, 'common.not_found'))).toBe(false)
  })

  it('shouldRetry_ServerError_RetriesTwice', () => {
    const error = new ApiError(500, 'common.unexpected')
    expect([0, 1, 2].map((count) => shouldRetry(count, error))).toEqual([true, true, false])
  })

  it('shouldRetry_NetworkError_Retries', () => {
    expect(shouldRetry(0, new TypeError('Failed to fetch'))).toBe(true)
  })
})
