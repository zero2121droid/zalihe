// Cuts a recording into scenes and joins them (STIL.md "Izvoz"): every scene becomes its own .mp4 under
// a temporary name, renamed only once ffmpeg exits with 0, and the scenes are joined with -c copy.
import { execFile } from 'node:child_process'
import { existsSync, mkdirSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import { renderEndcard, ENDCARD_SECONDS } from './endcard.js'
import { renderLogoPng } from './html.js'

const here = dirname(fileURLToPath(import.meta.url))
const exec = promisify(execFile)

export const encodeArgs = ['-r', '30', '-c:v', 'libx264', '-crf', '20', '-preset', 'veryfast', '-pix_fmt', 'yuv420p', '-movflags', '+faststart']

export const run = async (command, args, env) => {
  await exec(command, args, { maxBuffer: 1 << 26, env: { ...process.env, ...env } })
}

export async function probeDuration(file) {
  const { stdout } = await exec('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', file])
  return Number(stdout.trim())
}

/** Cuts [from, to] of the recording to a 1920x1080 scene, sped up by `speed` (at most 3x). */
export async function cutScene(raw, { from, to, speed = 1 }, target) {
  if (speed > 3) throw new Error('STIL.md: never faster than 3x.')
  const temporary = `${target}.part.mp4`
  await run('ffmpeg', ['-y', '-v', 'error', '-ss', String(from), '-to', String(to), '-i', raw, '-vf', `setpts=PTS/${speed}`, '-an', ...encodeArgs, temporary])
  renameSync(temporary, target)
}

/** Joins finished scenes without encoding them again. */
export async function concat(scenes, target) {
  const list = `${target}.txt`
  writeFileSync(list, scenes.map((file) => `file '${resolve(file).replaceAll("'", "'\\''")}'`).join('\n'))
  const temporary = `${target}.part.mp4`
  await run('ffmpeg', ['-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', list, '-c', 'copy', '-movflags', '+faststart', temporary])
  renameSync(temporary, target)
  rmSync(list)
}

/**
 * Scenes between record.js's marks. The recording starts a moment after the clock, so the marks are
 * shifted by that difference. `options` holds { speed, crop, zoom } per mark name.
 */
export async function scenesFromMarks(timeline, options = {}) {
  const shift = (await probeDuration(timeline.raw)) - timeline.duration
  return timeline.marks.map((mark, index) => ({
    name: mark.name,
    from: mark.at + shift,
    to: (timeline.marks[index + 1]?.at ?? timeline.duration) + shift,
    ...options[mark.name],
  }))
}

/** Frame by frame evidence for the check before handing a clip over. */
export async function extractCheckFrames(file, dir, count = 8) {
  rmSync(dir, { recursive: true, force: true })
  mkdirSync(dir, { recursive: true })
  const duration = await probeDuration(file)
  await run('ffmpeg', ['-y', '-v', 'error', '-i', file, '-vf', `fps=${count / duration}`, '-q:v', '3', join(dir, 'kadar-%02d.jpg')])
  return readdirSync(dir).map((name) => join(dir, name))
}

export async function extractPoster(file, target, at) {
  await run('ffmpeg', ['-y', '-v', 'error', '-ss', String(at ?? (await probeDuration(file)) / 2), '-i', file, '-frames:v', '1', '-q:v', '2', target])
}

/**
 * Renders a vertical clip with its end card.
 *   dir       the clip's folder; the result is <dir>/<name>.mp4 and <dir>/<name>-poster.jpg
 *   timeline  what record.js returned
 *   frame     { title: [line, line], message: [line, line] }
 *   scenes    [{ name, from, to, speed, crop, zoom }] with times in seconds of the recording
 *   phone     the recording is record.js's PHONE window, shown whole in a phone's screen
 */
export async function renderVertical({ dir, name, timeline, frame, scenes, posterAt, phone = false }) {
  const work = join(dir, 'scenes')
  rmSync(work, { recursive: true, force: true })
  mkdirSync(work, { recursive: true })

  const logoPng = join(here, 'assets/logo.png')
  if (!existsSync(logoPng)) await renderLogoPng(logoPng)
  const framePng = join(work, 'okvir.png')
  await run('python3', [join(here, 'vertical_frame.py'), framePng, logoPng, ...frame.title, ...frame.message, ...(phone ? ['phone'] : [])])

  const parts = []
  const chapters = []
  let clock = 0
  for (const [index, scene] of scenes.entries()) {
    const wide = join(work, `${index}-16x9.mp4`)
    await cutScene(timeline.raw, scene, wide)
    const part = join(work, `${index}.mp4`)
    await run(join(here, 'vertical.sh'), [wide, part, framePng, String(scene.crop ?? 232)], {
      ZOOM: scene.zoom ?? '',
      FADE_IN: index === 0 ? '1' : '0',
      PHONE: phone ? '1' : '0',
    })
    parts.push(part)
    chapters.push({ at: clock, name: scene.name })
    clock += await probeDuration(part)
  }

  const endcard = await renderEndcard({ phone })
  const closing = join(work, 'kraj.mp4')
  await run(join(here, 'vertical.sh'), [endcard, closing, framePng, '240'], { FADE_OUT: '1', PHONE: phone ? '1' : '0' })
  parts.push(closing)
  chapters.push({ at: clock, name: 'Završna kartica' })

  const output = join(dir, `${name}.mp4`)
  await concat(parts, output)
  await extractPoster(output, join(dir, `${name}-poster.jpg`), posterAt)
  const duration = await probeDuration(output)
  const frames = await extractCheckFrames(output, join(dir, 'kadrovi'))
  writeFileSync(join(dir, 'poglavlja.json'), JSON.stringify({ duration, chapters, endcard: ENDCARD_SECONDS }, null, 2))
  return { output, duration, chapters, frames }
}

export const printResult = ({ output, duration, chapters }) => {
  console.log(`${output}\n${duration.toFixed(1)} s`)
  for (const { at, name } of chapters) console.log(`${at.toFixed(1).padStart(5)} s  ${name}`)
}

