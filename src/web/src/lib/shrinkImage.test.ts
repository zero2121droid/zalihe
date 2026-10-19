import { describe, expect, it } from 'vitest'
import { fitWithin } from './shrinkImage'

describe('fitWithin', () => {
  it('FitWithin_LandscapePhoto_LongestSideBecomesTheMaximum', () => {
    expect(fitWithin(4032, 3024)).toEqual({ width: 800, height: 600 })
  })

  it('FitWithin_PortraitPhoto_KeepsProportions', () => {
    expect(fitWithin(1080, 1920)).toEqual({ width: 450, height: 800 })
  })

  it('FitWithin_SmallPicture_IsNotEnlarged', () => {
    expect(fitWithin(300, 200)).toEqual({ width: 300, height: 200 })
  })

  it('FitWithin_VeryThinPicture_KeepsAtLeastOnePixel', () => {
    expect(fitWithin(10000, 2)).toEqual({ width: 800, height: 1 })
  })
})
