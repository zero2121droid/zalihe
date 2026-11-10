// The closing card of every clip (STIL.md "Završna kartica"): 3.5 s at 1920x1080. Vertical clips put
// it into their frame with crop x=240, so the content stays inside the middle 1440 px.
import { mkdirSync, renameSync, existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { chromium } from 'playwright'
import { COLORS, logo, page, TMP, writePage } from './html.js'
import { encodeArgs, run } from './render.js'

export const ENDCARD_SECONDS = 3.5

export const ENDCARD_HTML = `<main class="endcard">
  ${logo(84)}
  <p class="lead">Sajt, Instagram, telefon i lokal na jednom mestu.</p>
  <p class="sub">Besplatno dok traje beta. Bez kartice.</p>
  <div class="cta">Napravi besplatan nalog</div>
</main>`

export const ENDCARD_CSS = `.endcard{width:1920px;height:1080px;display:flex;flex-direction:column;align-items:center;justify-content:center}
.endcard .lead{margin-top:52px;font-size:46px;font-weight:600}
.endcard .sub{margin-top:16px;font-size:32px;color:${COLORS.muted}}
.endcard .cta{margin-top:52px;padding:22px 44px;border-radius:4px;background:${COLORS.accent};color:${COLORS.bg};font-size:34px;font-weight:600}`

// For phone clips the card fills the phone's screen (780x1160), with the same content stacked narrower.
const PHONE_CSS = `.endcard{width:780px;height:1160px;padding:0 70px;text-align:center}
.endcard .lead{margin-top:56px;font-size:40px;line-height:1.25}
.endcard .sub{margin-top:20px;font-size:28px}
.endcard .cta{margin-top:56px;padding:22px 40px;font-size:32px}`

export async function renderEndcard({ phone = false } = {}) {
  const target = join(TMP, phone ? 'endcard-telefon.mp4' : 'endcard.mp4')
  if (existsSync(target)) return target
  mkdirSync(dirname(target), { recursive: true })
  const html = page(ENDCARD_HTML, ENDCARD_CSS + (phone ? PHONE_CSS : ''))
  const browser = await chromium.launch()
  const tab = await browser.newPage({ viewport: phone ? { width: 780, height: 1160 } : { width: 1920, height: 1080 } })
  await tab.goto(writePage(phone ? 'endcard-telefon.html' : 'endcard.html', html))
  await tab.evaluate(() => document.fonts.ready)
  const still = target.replace('.mp4', '.png')
  await tab.screenshot({ path: still })
  await browser.close()

  const temporary = `${target}.part.mp4`
  await run('ffmpeg', ['-y', '-v', 'error', '-loop', '1', '-framerate', '30', '-i', still, '-t', String(ENDCARD_SECONDS), '-an', ...encodeArgs, temporary])
  renameSync(temporary, target)
  return target
}
