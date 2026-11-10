// What the clip's own HTML pages share with the application: its fonts, colors and logo.
// logoMark.ts holds the real logo; it is read from there so a change of the logo reaches the clips.
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { chromium } from 'playwright'

const here = dirname(fileURLToPath(import.meta.url))
const webRoot = resolve(here, '../../src/web')
export const TMP = resolve(here, '../.tmp')

export const COLORS = {
  bg: '#11130E',
  surface: '#191C15',
  line: '#2C3125',
  text: '#E4E7DC',
  text2: '#C4C9B8',
  muted: '#9BA18F',
  accent: '#B4C17E',
  danger: '#E07A5F',
}

const fontFace = (family, pkg, weights) =>
  ['latin-ext', 'latin']
    .flatMap((subset) =>
      weights.map((weight) => {
        const file = join(webRoot, 'node_modules/@fontsource', pkg, 'files', `${pkg}-${subset}-${weight}-normal.woff2`)
        return `@font-face{font-family:'${family} ${subset}';font-weight:${weight};src:url('${pathToFileURL(file)}') format('woff2')}`
      }),
    )
    .join('\n')

export const FONTS = `${fontFace('IBM Plex Sans', 'ibm-plex-sans', [400, 500, 600, 700])}
${fontFace('IBM Plex Mono', 'ibm-plex-mono', [400, 500, 600])}`

export const SANS = `'IBM Plex Sans latin','IBM Plex Sans latin-ext',sans-serif`
export const MONO = `'IBM Plex Mono latin','IBM Plex Mono latin-ext',monospace`

const logoSource = readFileSync(join(webRoot, 'src/components/logoMark.ts'), 'utf8')
const LID = logoSource.match(/MARK_LID = '([^']+)'/)[1]
const BODY = logoSource.match(/MARK_BODY = '([^']+)'/)[1]

export const logoMark = (size, color = COLORS.accent) =>
  `<svg width="${size}" height="${size}" viewBox="0 0 16 16" fill="${color}" style="display:block;flex:none"><path d="${LID}"/><path fill-rule="evenodd" d="${BODY}"/></svg>`

/** The logo as in the app: the mark, then "lagerko" in semibold mono. The sizes keep Logo.tsx's 16:17 proportions. */
export const logo = (fontSize) =>
  `<div style="display:flex;align-items:center;gap:${(fontSize * 10) / 17}px">${logoMark((fontSize * 16) / 17)}<span style="font:600 ${fontSize}px ${MONO};letter-spacing:-.02em;color:${COLORS.text}">lagerko</span></div>`

export const page = (body, css = '') => `<!doctype html><meta charset="utf-8"><style>${FONTS}
*{box-sizing:border-box;margin:0}html,body{background:${COLORS.bg};color:${COLORS.text};font-family:${SANS};overflow:hidden}${css}</style>${body}`

/** Writes a page next to the temporary files, because file:// pages may load fonts from disk. */
export function writePage(name, html) {
  mkdirSync(TMP, { recursive: true })
  const file = join(TMP, name)
  writeFileSync(file, html)
  return pathToFileURL(file).href
}

/** Renders the logo to a transparent PNG, for vertical_frame.py. */
export async function renderLogoPng(target, width = 280) {
  mkdirSync(dirname(target), { recursive: true })
  const browser = await chromium.launch()
  const tab = await browser.newPage({ viewport: { width: 800, height: 200 }, deviceScaleFactor: 2 })
  await tab.goto(writePage('logo.html', page(`<div id="l" style="display:inline-block">${logo(56)}</div>`, 'html,body{background:transparent}')))
  await tab.evaluate(() => document.fonts.ready)
  await tab.locator('#l').screenshot({ path: target, omitBackground: true })
  await browser.close()
}
