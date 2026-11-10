// Records a page with a render(t) function frame by frame: Playwright screenshots (JPEG, quality 92,
// 30 fps) go straight to ffmpeg through image2pipe, so animation timing never depends on machine speed.
import { spawn } from 'node:child_process'
import { renameSync } from 'node:fs'
import { chromium } from 'playwright'
import { encodeArgs } from './render.js'

const FPS = 30

export async function openStage(url, { width, height }) {
  const browser = await chromium.launch()
  const tab = await browser.newPage({ viewport: { width, height } })
  await tab.goto(url, { waitUntil: 'networkidle' })
  await tab.evaluate(() => document.fonts.ready)
  await tab.evaluate(() => Promise.all([...document.images].map((image) => image.decode().catch(() => {}))))
  return { browser, tab }
}

/** Screenshots at the given times, to check the composition before a full render. */
export async function stills(url, size, times, pathFor) {
  const { browser, tab } = await openStage(url, size)
  for (const t of times) {
    await tab.evaluate((at) => window.render(at), t)
    await tab.screenshot({ path: pathFor(t), type: 'jpeg', quality: 92 })
  }
  await browser.close()
}

export async function renderFrames(url, size, duration, target) {
  const { browser, tab } = await openStage(url, size)
  const temporary = `${target}.part.mp4`
  const ffmpeg = spawn('ffmpeg', ['-y', '-v', 'error', '-f', 'image2pipe', '-framerate', String(FPS), '-c:v', 'mjpeg', '-i', '-', '-vf', 'scale=in_range=pc:out_range=tv,format=yuv420p', '-an', ...encodeArgs, temporary], {
    stdio: ['pipe', 'inherit', 'inherit'],
  })
  const finished = new Promise((resolve, reject) => {
    ffmpeg.on('error', reject)
    ffmpeg.on('close', (code) => (code === 0 ? resolve() : reject(new Error(`ffmpeg exited with ${code}`))))
  })
  const frames = Math.round(duration * FPS)
  for (let frame = 0; frame < frames; frame++) {
    await tab.evaluate((at) => window.render(at), frame / FPS)
    const jpeg = await tab.screenshot({ type: 'jpeg', quality: 92 })
    if (!ffmpeg.stdin.write(jpeg)) await new Promise((resume) => ffmpeg.stdin.once('drain', resume))
  }
  ffmpeg.stdin.end()
  await finished
  await browser.close()
  renameSync(temporary, target)
}
