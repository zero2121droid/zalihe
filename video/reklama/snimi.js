// Ad "Problem → rešenje": 18 s of motion graphics in the app's colors, not a recording of the app.
// `node reklama/snimi.js --stills` writes only the check frames.
import { mkdirSync, rmSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { renderFrames, stills } from '../lib/frames.js'
import { COLORS, logo, logoMark, MONO, page, writePage } from '../lib/html.js'
import { extractCheckFrames, extractPoster, printResult, probeDuration } from '../lib/render.js'

const dir = dirname(fileURLToPath(import.meta.url))
const SIZE = { width: 1080, height: 1920 }
const DURATION = 18
const CARDS = [
  { id: 'sajt', label: 'SAJT · NIT.RS' },
  { id: 'ig', label: 'INSTAGRAM' },
  { id: 'lokal', label: 'LOKAL' },
]
const TEXTS = [
  { from: 0.1, to: 3.4, lines: ['Prodaješ na', 'tri mesta?'], sub: 'Ista majica, tri lagera.' },
  { from: 3.4, to: 5.4, lines: ['Na Instagramu', 'neko kupi 4.'] },
  { from: 5.4, to: 7.4, lines: ['A sajt i lokal', 'i dalje kažu 8.'], sub: 'Niko ih nije ispravio.' },
  { from: 7.4, to: 9.3, lines: ['Pa prodaš robu', 'koju nemaš.'], sub: 'Kupac plati, a majice nema.' },
  { from: 9.7, to: 12.0, lines: ['Sa Lagerkom', 'porudžbina skine 4…'] },
  { from: 12.0, to: 14.3, lines: ['…i to piše', 'svuda odjednom.'] },
]

const html = page(
  `<div id="top">${logo(38)}</div>
  ${TEXTS.map(
    (text, i) => `<div class="text" id="t${i}"><h1>${text.lines[0]}<br>${text.lines[1]}</h1>${text.sub ? `<p>${text.sub}</p>` : ''}</div>`,
  ).join('')}
  ${CARDS.map(
    (card) => `<article class="card" id="${card.id}">
      <div><span class="label">${card.label}</span><strong>Majica Basic, bela – M</strong></div>
      <div class="stock"><div class="count"><b>8</b><small>kom</small></div><em></em></div>
    </article>`,
  ).join('')}
  <div class="hub" id="hub"><i class="wire up"></i><i class="wire down"></i><div class="disc">${logoMark(84)}</div></div>
  <section id="end">
    ${logo(76)}
    <h1>Znaj koliko robe<br>stvarno imaš.</h1>
    <div class="cta">Napravi besplatan nalog</div>
    <p>Besplatno dok traje beta · bez kartice</p>
  </section>
  <div id="fade"></div>
  <script>
    const COLORS = ${JSON.stringify(COLORS)}, TEXTS = ${JSON.stringify(TEXTS)}, CARDS = ${JSON.stringify(CARDS)}, DURATION = ${DURATION}
    const clamp = (v) => Math.min(1, Math.max(0, v))
    const out = (v) => 1 - (1 - clamp(v)) ** 3
    const inOut = (v) => { v = clamp(v); return v < 0.5 ? 2 * v * v : 1 - (-2 * v + 2) ** 2 / 2 }
    const $ = (id) => document.getElementById(id)
    const TRANSITION = 0.45

    // Enters over 0.45 s rising 40 px, leaves the same way; returns [opacity, y].
    const reveal = (t, from, to) => {
      const enter = out((t - from) / TRANSITION)
      const leave = out((t - (to - TRANSITION)) / TRANSITION)
      return [enter * (1 - leave), (1 - enter) * 40 - leave * 40]
    }
    const count = (t, at, from, to) => Math.round(from + (to - from) * inOut((t - at) / 0.7))

    // The stack during the problem, then with the hub in the gap under the first card.
    const PROBLEM_Y = [660, 910, 1160], SOLUTION_Y = [640, 1090, 1340]

    const card = (id, { x, y, opacity, value, color, border, tag, tagColor }) => {
      const el = $(id)
      el.style.transform = 'translate(' + x + 'px,' + y + 'px)'
      el.style.opacity = opacity
      el.style.borderColor = border
      const number = el.querySelector('b')
      number.textContent = value
      number.style.color = color
      const label = el.querySelector('em')
      label.textContent = tag
      label.style.color = tagColor
    }

    window.render = (t) => {
      TEXTS.forEach((text, i) => {
        const [opacity, y] = reveal(t, text.from, text.to)
        const el = $('t' + i)
        el.style.opacity = opacity
        el.style.transform = 'translateY(' + y + 'px)'
      })
      const problem = t < 9.6
      CARDS.forEach((c, i) => {
        const state = { value: 8, color: COLORS.text, border: COLORS.line, tag: '', tagColor: COLORS.muted }
        let x, y, opacity
        if (problem) {
          const enter = out((t - (0.45 + i * 0.28)) / 0.55)
          const leave = out((t - 9.2) / 0.4)
          x = 90 + (1 - enter) * 1100 - leave * 60
          y = PROBLEM_Y[i]
          opacity = enter * (1 - leave)
          if (c.id === 'ig' && t >= 3.6) {
            state.value = count(t, 3.7, 8, 4)
            state.border = COLORS.accent
            state.color = COLORS.accent
            state.tag = 'prodato 4'
            state.tagColor = COLORS.accent
          }
          if (c.id !== 'ig' && t >= 5.6) {
            state.color = COLORS.danger
            state.border = COLORS.danger
            state.tag = 'netačno'
            state.tagColor = COLORS.danger
          }
          if (c.id === 'sajt' && t >= 7.6) {
            state.tag = 'nova porudžbina: 6'
            const shake = t - 7.75
            if (shake > 0 && shake < 0.9) x += Math.sin(shake * 42) * 14 * (1 - shake / 0.9)
          }
        } else {
          const enter = out((t - (9.85 + i * 0.22)) / 0.55)
          const leave = out((t - 14.2) / 0.35)
          x = 90 - (1 - enter) * 1100
          y = SOLUTION_Y[i]
          opacity = enter * (1 - leave)
          state.value = count(t, 12, 8, 4)
          const done = inOut((t - 12) / 0.7)
          if (done > 0) state.border = COLORS.accent
          if (done > 0) state.color = COLORS.accent
          if (done >= 1) {
            state.tag = '✓ isto stanje'
            state.tagColor = COLORS.accent
          }
        }
        card(c.id, { x, y, opacity, ...state })
      })

      const hubIn = out((t - 10.3) / 0.5) * (1 - out((t - 14.2) / 0.35))
      const pulse = t > 11.9 && t < 12.9 ? Math.sin(((t - 11.9) / 1) * Math.PI) : 0
      const hub = $('hub')
      hub.style.opacity = hubIn
      hub.querySelector('.disc').style.transform = 'scale(' + (1 + pulse * 0.14) + ')'
      hub.querySelector('.disc').style.boxShadow = '0 0 0 ' + pulse * 26 + 'px rgba(180,193,126,' + pulse * 0.22 + ')'

      const [endOpacity, endY] = reveal(t, 14.6, 40)
      $('end').style.opacity = endOpacity
      $('end').style.transform = 'translateY(' + endY + 'px)'
      $('top').style.opacity = 1 - out((t - 14.2) / 0.35)
      $('fade').style.opacity = Math.max(1 - t / (10 / 30), clamp((t - (DURATION - 0.5)) / 0.5))
    }
  </script>`,
  `body{width:1080px;height:1920px;position:relative}
   #top{position:absolute;left:90px;top:130px}
   .text{position:absolute;left:90px;top:300px;width:960px}
   .text h1{font-size:84px;line-height:1.14;font-weight:700;letter-spacing:-.015em}
   .text p{margin-top:24px;font-size:40px;color:${COLORS.muted}}
   .card{position:absolute;left:0;top:0;width:900px;height:230px;padding:0 44px;display:flex;align-items:center;justify-content:space-between;
         background:${COLORS.surface};border:2px solid ${COLORS.line};border-radius:22px}
   .label{display:block;font:500 25px ${MONO};letter-spacing:.1em;color:${COLORS.muted}}
   .card strong{display:block;margin-top:18px;font-size:34px;font-weight:600}
   .stock{display:flex;flex-direction:column;align-items:flex-end;min-width:290px}
   .count{display:flex;align-items:baseline;gap:12px}
   .count b{font:500 104px/1 ${MONO};font-variant-numeric:tabular-nums}
   .count small{font:400 30px ${MONO};color:${COLORS.muted}}
   .stock em{height:36px;margin-top:10px;font-style:normal;font-size:27px;font-weight:500;white-space:nowrap}
   .hub{position:absolute;left:0;top:870px;width:1080px;height:220px;display:flex;align-items:center;justify-content:center}
   .disc{position:relative;width:176px;height:176px;border-radius:50%;background:${COLORS.surface};border:2px solid ${COLORS.accent};display:flex;align-items:center;justify-content:center}
   .wire{position:absolute;left:539px;width:2px;height:22px;background:${COLORS.accent}}
   .wire.up{top:0}.wire.down{bottom:0}
   #end{position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center}
   #end h1{margin-top:72px;font-size:88px;line-height:1.14;font-weight:700;letter-spacing:-.015em}
   .cta{margin-top:72px;padding:30px 56px;border-radius:6px;background:${COLORS.accent};color:${COLORS.bg};font-size:42px;font-weight:600}
   #end p{margin-top:40px;font-size:36px;font-weight:500;color:${COLORS.muted}}
   #fade{position:absolute;inset:0;background:#000}`,
)
const url = writePage('reklama.html', html)

if (process.argv.includes('--stills')) {
  const work = join(dir, 'kadrovi')
  rmSync(work, { recursive: true, force: true })
  mkdirSync(work, { recursive: true })
  await stills(url, SIZE, [1.5, 4.8, 6.5, 8.0, 10.6, 12.8, 16.5], (t) => join(work, `kadar-${String(t).padStart(4, '0')}.jpg`))
} else {
  const output = join(dir, 'reklama.mp4')
  await renderFrames(url, SIZE, DURATION, output)
  await extractPoster(output, join(dir, 'reklama-poster.jpg'), 12.8)
  await extractCheckFrames(output, join(dir, 'kadrovi'))
  printResult({
    output,
    duration: await probeDuration(output),
    chapters: [
      { at: 0, name: 'Tri mesta, tri lagera' },
      { at: 3.4, name: 'Prodaja na Instagramu' },
      { at: 5.4, name: 'Sajt i lokal netačni' },
      { at: 7.4, name: 'Prodato što nemaš' },
      { at: 9.7, name: 'Sa Lagerkom' },
      { at: 14.6, name: 'Završna poruka' },
    ],
  })
}
