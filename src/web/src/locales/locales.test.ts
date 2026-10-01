import { describe, expect, it } from 'vitest'
import en from './en.json'
import srLatn from './sr-Latn.json'

function keys(value: object, prefix = ''): string[] {
  return Object.entries(value).flatMap(([key, child]) =>
    typeof child === 'object' && child !== null ? keys(child, `${prefix}${key}.`) : [`${prefix}${key}`],
  )
}

describe('locales', () => {
  it('Keys_BothLanguages_AreTheSame', () => {
    // Act
    const serbian = keys(srLatn).sort()
    const english = keys(en).sort()

    // Assert
    expect(english).toEqual(serbian)
  })

  it('Values_BothLanguages_AreNotEmpty', () => {
    // Arrange
    const values = [srLatn, en].flatMap((locale) =>
      keys(locale).map((key) => key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], locale)),
    )

    // Assert
    expect(values.every((value) => typeof value === 'string' && value.trim() !== '')).toBe(true)
  })
})
