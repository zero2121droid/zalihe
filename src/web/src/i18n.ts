import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from './locales/en.json'
import srLatn from './locales/sr-Latn.json'

export const languages = ['sr-Latn', 'en'] as const
export type Language = (typeof languages)[number]
export const defaultLanguage: Language = 'sr-Latn'

const STORAGE_KEY = 'zalihe.language'

export function isLanguage(value: unknown): value is Language {
  return languages.includes(value as Language)
}

function storedLanguage(): Language {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return isLanguage(value) ? value : defaultLanguage
  } catch {
    return defaultLanguage
  }
}

/**
 * Switches the UI language. Before sign-in the choice is remembered in the browser;
 * after sign-in the user's saved language (User.Language) is applied on load.
 */
export async function setLanguage(language: Language) {
  try {
    localStorage.setItem(STORAGE_KEY, language)
  } catch {
    // Storage can be unavailable (private mode); the choice then lasts for this visit only.
  }
  await i18n.changeLanguage(language)
}

i18n.on('languageChanged', (language) => {
  document.documentElement.lang = language
})

void i18n.use(initReactI18next).init({
  resources: {
    'sr-Latn': { translation: srLatn },
    en: { translation: en },
  },
  lng: storedLanguage(),
  fallbackLng: defaultLanguage,
  interpolation: { escapeValue: false },
  returnNull: false,
})

export default i18n
