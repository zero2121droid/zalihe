import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../api/errors'
import { useGetCurrentUser } from '../api/generated/auth/auth'
import { isLanguage, setLanguage } from '../i18n'

/**
 * The signed-in user, or `isAnonymous` when the API answers 401.
 * Also applies the user's saved language to the UI.
 */
export function useCurrentUser() {
  const { i18n } = useTranslation()
  const query = useGetCurrentUser({
    query: {
      staleTime: 5 * 60 * 1000,
      // A 401 stays valid when the other route guard mounts; sign-in invalidates the query anyway.
      retryOnMount: false,
      retry: (failureCount, error) => !(error instanceof ApiError && error.status < 500) && failureCount < 2,
    },
  })

  const language = query.data?.language
  useEffect(() => {
    if (isLanguage(language) && language !== i18n.language) {
      void setLanguage(language)
    }
  }, [language, i18n.language])

  const isAnonymous = query.error instanceof ApiError && query.error.status === 401
  return { ...query, user: query.data, isAnonymous }
}
