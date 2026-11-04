import { describe, expect, it } from 'vitest'
import globalCss from './global.css?raw'
import { theme } from './theme'

describe('input text size', () => {
  it('Input_ThemeVars_UseTheResponsiveToken', () => {
    const vars = theme.components?.Input?.vars?.(theme as never, {} as never, {} as never)

    expect(vars?.wrapper?.['--input-fz']).toBe('var(--z-input-text)')
  })

  it('InputTextToken_OnTouchScreens_IsLargeEnoughThatIosDoesNotZoom', () => {
    const touchRule = /@media \(max-width: 47\.99em\), \(pointer: coarse\) \{\s*:root \{\s*--z-input-text: (\d+)px/.exec(globalCss)

    expect(Number(touchRule?.[1])).toBeGreaterThanOrEqual(16)
  })
})
