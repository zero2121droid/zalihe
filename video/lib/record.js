// Records a scenario of the running application with Playwright, at 1920x1080 or at the window of vertical clips. Clicks and typing are
// slow enough to read, and a visible cursor is drawn into the page because recordings don't show one.
//
//   const clip = await startRecording({ dir: 'video/porudzbine/raw', path: '/' })
//   await clip.click(page.getByRole('link', { name: 'Porudžbine' }))
//   clip.mark('forma')                          // start of a scene, for render.js
//   const timeline = await clip.stop()
import { mkdirSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'
import { DEMO_LOGIN } from '../seed-demo.js'
import { captionScript, showCaption } from './captions.js'

export const APP_URL = process.env.APP_URL ?? 'http://localhost:5173'
export const VIDEO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
export const WIDE = { width: 1920, height: 1080 }
/**
 * Vertical clips show 1080 px of the page beside the navigation (232 px). In this window the app's
 * content fills exactly that crop, right-aligned numbers included, at its real size with no scaling.
 */
export const VERTICAL = { width: 1312, height: 810 }
/**
 * Phone clips: the app's mobile layout in a 390x580 window. Chrome is started at 2x, so the recording
 * is 780x1160 with sharp text (an emulated deviceScaleFactor would record at CSS size).
 */
export const PHONE = { width: 390, height: 580, scale: 2 }
const TYPE_DELAY = 60

const cursorScript = `(${(() => {
  const start = () => {
    if (document.getElementById('video-cursor')) return
    const cursor = document.createElement('div')
    cursor.id = 'video-cursor'
    cursor.innerHTML = `<svg width="30" height="30" viewBox="0 0 24 24"><path d="M4 2.5v17.2l4.9-4.6 3.2 7 3-1.4-3.2-6.9h6.6z" fill="#E4E7DC" stroke="#11130E" stroke-width="1.6" stroke-linejoin="round"/></svg>`
    cursor.style.cssText = 'position:fixed;left:0;top:0;z-index:2147483647;pointer-events:none;transform:translate(960px,540px)'
    // On a phone a finger, not an arrow: a soft dot centered on the point.
    if (window.__touch) {
      cursor.innerHTML = '<div style="width:34px;height:34px;margin:-17px 0 0 -17px;border-radius:50%;background:rgba(228,231,220,.28);border:2px solid rgba(228,231,220,.75)"></div>'
    }
    const ring = document.createElement('div')
    ring.style.cssText = `position:fixed;left:0;top:0;width:44px;height:44px;margin:-22px 0 0 -22px;border-radius:50%;
      border:3px solid #B4C17E;opacity:0;z-index:2147483646;pointer-events:none`
    document.documentElement.append(ring, cursor)
    addEventListener('mousemove', (e) => (cursor.style.transform = `translate(${e.clientX}px,${e.clientY}px)`), true)
    addEventListener(
      'mousedown',
      (e) => {
        ring.style.left = `${e.clientX}px`
        ring.style.top = `${e.clientY}px`
        ring.animate([{ opacity: 0.9, transform: 'scale(.4)' }, { opacity: 0, transform: 'scale(1.2)' }], { duration: 420, easing: 'ease-out' })
      },
      true,
    )
  }
  if (document.documentElement) start()
  else addEventListener('DOMContentLoaded', start)
}).toString()})()`

async function login(browser) {
  const context = await browser.newContext({ viewport: WIDE, locale: 'sr-Latn' })
  const response = await context.request.post(`${APP_URL}/api/auth/login`, { data: DEMO_LOGIN })
  if (!response.ok()) throw new Error(`Demo login failed (${response.status()}). Run: node video/seed-demo.js`)
  const state = await context.storageState()
  await context.close()
  return state
}

/** Opens the app logged in as Ana, on the dark theme, and starts recording once the page has settled. */
export async function startRecording({ dir, path = '/', record = true, viewport = VERTICAL }) {
  const phone = viewport === PHONE
  const output = resolve(dir)
  rmSync(output, { recursive: true, force: true })
  mkdirSync(output, { recursive: true })

  const browser = await chromium.launch(phone ? { args: [`--force-device-scale-factor=${PHONE.scale}`] } : {})
  const storageState = await login(browser)
  const recordSize = phone ? { width: PHONE.width * PHONE.scale, height: PHONE.height * PHONE.scale } : viewport
  const context = await browser.newContext({
    viewport: { width: viewport.width, height: viewport.height },
    locale: 'sr-Latn',
    colorScheme: 'dark',
    storageState,
    ...(record && { recordVideo: { dir: output, size: recordSize } }),
  })
  // Clips show the shop's public address, not the local test shop (noted in each clip's NAPOMENE.md).
  await context.route('**/api/**', async (route) => {
    if (route.request().method() !== 'GET') return route.continue()
    const response = await route.fetch()
    if (!response.headers()['content-type']?.includes('json')) return route.fulfill({ response })
    const body = (await response.text()).replaceAll('"http://localhost:8080"', '"https://nit.rs"')
    return route.fulfill({ response, body })
  })
  if (phone) await context.addInitScript(() => (window.__touch = true))
  await context.addInitScript(cursorScript)
  await context.addInitScript(captionScript)

  const page = await context.newPage()
  const begun = Date.now()
  const marks = []
  let cursor = phone ? { x: 300, y: 420 } : { x: 600, y: 400 }

  const clock = () => (Date.now() - begun) / 1000
  const pause = (ms = 750) => page.waitForTimeout(ms)

  const moveTo = async (x, y) => {
    const from = cursor
    const distance = Math.hypot(x - from.x, y - from.y)
    const steps = Math.max(10, Math.min(36, Math.round(distance / 22)))
    for (let step = 1; step <= steps; step++) {
      const t = step / steps
      const eased = t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2
      await page.mouse.move(from.x + (x - from.x) * eased, from.y + (y - from.y) * eased)
      await page.waitForTimeout(12)
    }
    cursor = { x, y }
  }

  const hover = async (locator) => {
    await locator.first().scrollIntoViewIfNeeded()
    const box = await locator.first().boundingBox()
    if (!box) throw new Error(`Nothing to point at: ${locator}`)
    await moveTo(box.x + box.width / 2, box.y + box.height / 2)
  }

  const clip = {
    page,
    pause,
    hover,
    moveTo,
    now: clock,
    goto: async (to) => {
      await page.goto(`${APP_URL}${to}`, { waitUntil: 'networkidle' })
    },
    /** Starts a scene: render.js cuts the recording at these marks. */
    mark: (name) => marks.push({ name, at: clock() }),
    caption: (text, centerX, bottom) => showCaption(page, text, centerX, bottom),
    click: async (locator, { after = 750 } = {}) => {
      await hover(locator)
      await page.waitForTimeout(220)
      await page.mouse.down()
      await page.waitForTimeout(70)
      await page.mouse.up()
      await pause(after)
    },
    type: async (locator, text, { after = 750 } = {}) => {
      await clip.click(locator, { after: 180 })
      await page.keyboard.type(text, { delay: TYPE_DELAY })
      await pause(after)
    },
    /** Replaces what a field already holds, the way a person selects it first. */
    replace: async (locator, text, { after = 750 } = {}) => {
      await clip.click(locator, { after: 150 })
      await page.keyboard.press('ControlOrMeta+A')
      await page.keyboard.type(text, { delay: TYPE_DELAY })
      await pause(after)
    },
    /** Eases the page to a position, so a scroll reads as a camera move and not a jump. */
    scrollTo: async (y, duration = 900) => {
      await page.evaluate(
        ([target, ms]) =>
          new Promise((done) => {
            const scroller = document.scrollingElement
            const from = scroller.scrollTop
            const begin = performance.now()
            const frame = (time) => {
              const t = Math.min(1, (time - begin) / ms)
              scroller.scrollTop = from + (target - from) * (t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2)
              if (t < 1) requestAnimationFrame(frame)
              else done()
            }
            requestAnimationFrame(frame)
          }),
        [y, duration],
      )
    },
    /** Ends the recording and returns { raw, duration, marks } for render.js. */
    stop: async () => {
      const duration = clock()
      await context.close()
      await browser.close()
      if (!record) return { raw: null, duration, marks }
      const file = readdirSync(output).find((name) => name.endsWith('.webm'))
      const raw = join(output, 'snimak.webm')
      renameSync(join(output, file), raw)
      const timeline = { raw, size: statSync(raw).size, duration, marks }
      writeFileSync(join(output, 'timeline.json'), JSON.stringify(timeline, null, 2))
      return timeline
    },
  }

  await page.goto(`${APP_URL}${path}`, { waitUntil: 'networkidle' })
  await page.evaluate(() => document.fonts.ready)
  // The logo's landing animation and the page's entrance finish before the first scene.
  await pause(1600)
  return clip
}
