// "Obilazak funkcija" (16:9): real screens with a slow camera, rendered frame by frame from render(t).
// Run obilazak/ekrani.js first. `node obilazak/snimi.js --stills` only writes check frames.
import { mkdirSync, rmSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { ENDCARD_CSS, ENDCARD_HTML } from '../lib/endcard.js'
import { renderFrames, stills } from '../lib/frames.js'
import { COLORS, logo, MONO, page, writePage } from '../lib/html.js'
import { extractCheckFrames, extractPoster, printResult, probeDuration } from '../lib/render.js'

const dir = dirname(fileURLToPath(import.meta.url))
const SIZE = { width: 1920, height: 1080 }

// x and y: where the camera heads, in the screenshot's pixels; zoom: how far it gets.
const SCENES = [
  { image: '1-pocetna', title: 'Početna', text: 'Vrednost zaliha, šta treba naručiti i poslednja kretanja.', x: 760, y: 170, zoom: 1.2 },
  { image: '2-artikli', title: 'Artikli', text: 'Stanje, minimum i status svakog artikla.', x: 1150, y: 200, zoom: 1.18 },
  { image: '3-artikal', title: 'Kartica artikla', text: 'Stanje, minimum, prodaja za 30 dana i grafikon.', x: 760, y: 230, zoom: 1.25 },
  { image: '4-porudzbine', title: 'Porudžbine', text: 'Sajt, Instagram i ručne porudžbine na jednom mestu.', x: 1000, y: 180, zoom: 1.2 },
  { image: '5-sajt', title: 'Prodavnica na sajtu', text: 'Stanje na sajtu prati lager, bez ručne izmene.', x: 1250, y: 420, zoom: 1.3 },
  { image: '6-prijem', title: 'Prijem robe', text: 'Cela faktura ulazi odjednom.', x: 900, y: 280, zoom: 1.2 },
  { image: '7-izvestaji', title: 'Izveštaji', text: 'Šta se prodaje, a šta stoji na polici.', x: 1000, y: 180, zoom: 1.18 },
  { image: '8-istorija', title: 'Istorija artikla', text: 'Svaka promena ostaje zapisana: ko, kada i zašto.', x: 1000, y: 120, zoom: 1.2 },
]
const INTRO = 2.5
const FADE = 0.5
const STEP = 3.9
const SCENE = STEP + FADE
const SCENES_START = INTRO - FADE
const OUTRO_START = SCENES_START + SCENES.length * STEP
const DURATION = OUTRO_START + FADE + 3.5

const dashes = (index) => SCENES.map((_, i) => `<i class="${i <= index ? 'on' : ''}"></i>`).join('')
const html = page(
  `<section id="intro" class="layer center">${logo(84)}<p>Kratak obilazak aplikacije</p></section>
  ${SCENES.map(
    (scene, index) => `<section class="layer scene" id="s${index}">
      <img src="${pathToFileURL(join(dir, 'ekrani', `${scene.image}.png`))}" style="transform-origin:${scene.x}px ${scene.y}px">
      <div class="card"><span>${String(index + 1).padStart(2, '0')} / ${String(SCENES.length).padStart(2, '0')}</span><h2>${scene.title}</h2><p>${scene.text}</p></div>
      <div class="dashes">${dashes(index)}</div>
    </section>`,
  ).join('')}
  <section id="outro" class="layer">${ENDCARD_HTML}</section>
  <div id="fade"></div>
  <script>
    const SCENES = ${JSON.stringify(SCENES)}, FADE = ${FADE}, SCENE = ${SCENE}, STEP = ${STEP}
    const START = ${SCENES_START}, OUTRO = ${OUTRO_START}, DURATION = ${DURATION}
    const clamp = (v) => Math.min(1, Math.max(0, v))
    const ease = (v) => (v < 0.5 ? 2 * v * v : 1 - (-2 * v + 2) ** 2 / 2)
    const $ = (id) => document.getElementById(id)
    window.render = (t) => {
      $('intro').style.opacity = 1 - clamp((t - START) / FADE)
      SCENES.forEach((scene, i) => {
        const local = t - (START + i * STEP)
        const layer = $('s' + i)
        const visible = local > 0 && local < SCENE
        layer.style.display = visible ? 'block' : 'none'
        if (!visible) return
        layer.style.opacity = clamp(local / FADE)
        layer.style.zIndex = i + 1
        layer.querySelector('img').style.transform = 'scale(' + (1 + (scene.zoom - 1) * ease(clamp(local / SCENE))) + ')'
        const card = layer.querySelector('.card')
        const shown = ease(clamp((local - FADE) / 0.45))
        card.style.opacity = shown
        card.style.transform = 'translateY(' + (1 - shown) * 24 + 'px)'
      })
      $('outro').style.opacity = clamp((t - OUTRO) / FADE)
      $('fade').style.opacity = Math.max(1 - t / (10 / 30), clamp((t - (DURATION - 0.5)) / 0.5))
    }
  </script>`,
  `.layer{position:absolute;inset:0;width:1920px;height:1080px;overflow:hidden;background:${COLORS.bg}}
   .center{display:flex;flex-direction:column;align-items:center;justify-content:center}
   #intro p{margin-top:34px;font-size:40px;color:${COLORS.text2}}
   #outro{z-index:100}
   #fade{position:absolute;inset:0;background:#000;z-index:200}
   .scene img{position:absolute;inset:0;width:1920px;height:1080px}
   .card{position:absolute;left:56px;bottom:56px;max-width:1100px;padding:28px 36px;background:${COLORS.surface};border:1px solid ${COLORS.line};border-radius:6px}
   .card span{display:block;font:500 22px ${MONO};letter-spacing:.08em;color:${COLORS.accent}}
   .card h2{margin-top:10px;font-size:46px;font-weight:700;letter-spacing:-.01em}
   .card p{margin-top:8px;font-size:28px;color:${COLORS.text2}}
   .dashes{position:absolute;right:56px;bottom:56px;display:flex;gap:8px;padding:12px 14px;background:${COLORS.surface};border:1px solid ${COLORS.line};border-radius:6px}
   .dashes i{width:44px;height:6px;border-radius:3px;background:${COLORS.line}}
   .dashes i.on{background:${COLORS.accent}}
   ${ENDCARD_CSS}`,
)
const url = writePage('obilazak.html', html)

if (process.argv.includes('--stills')) {
  const work = join(dir, 'kadrovi')
  rmSync(work, { recursive: true, force: true })
  mkdirSync(work, { recursive: true })
  await stills(url, SIZE, [1.2, 5, 9, 12.8, 16.8, 20.5, 24.5, 28.4, 32, 35.5], (t) => join(work, `kadar-${String(t).padStart(4, '0')}.jpg`))
} else {
  const output = join(dir, 'obilazak.mp4')
  await renderFrames(url, SIZE, DURATION, output)
  await extractPoster(output, join(dir, 'obilazak-poster.jpg'), SCENES_START + 2 * STEP + 3)
  await extractCheckFrames(output, join(dir, 'kadrovi'))
  printResult({
    output,
    duration: await probeDuration(output),
    chapters: [
      { at: 0, name: 'Uvod' },
      ...SCENES.map((scene, i) => ({ at: SCENES_START + i * STEP + FADE, name: scene.title })),
      { at: OUTRO_START, name: 'Završna kartica' },
    ],
  })
}
