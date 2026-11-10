// Clip "Uvoz iz CSV-a": a spreadsheet becomes items with stock and minimums.
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { seed } from '../seed-demo.js'
import { startRecording } from '../lib/record.js'
import { printResult, renderVertical, scenesFromMarks } from '../lib/render.js'

const dir = dirname(fileURLToPath(import.meta.url))
// The items table needs about 1110 px; the usual window (1080 px beside the navigation) would cut its
// last column. This one gives 1208 px, scaled into the frame's 1080x810 (about 0.9x).
const WINDOW = { width: 1440, height: 906 }
const CAPTION_X = 232 + (WINDOW.width - 232) / 2
seed('base')

const clip = await startRecording({ dir: join(dir, 'raw'), path: '/items', viewport: WINDOW })
const { page } = clip

clip.mark('Artikli')
await clip.caption('Već imaš artikle u tabeli? Ne kucaš ih ponovo.', CAPTION_X)
await clip.pause(600)
await clip.click(page.getByRole('link', { name: 'Uvezi CSV' }).or(page.getByRole('button', { name: 'Uvezi CSV' })), { after: 500 })

clip.mark('Izbor fajla')
await page.locator('input[type=file]').setInputFiles(join(dir, 'demo-artikli.csv'))
await page.getByRole('button', { name: /Proveri/ }).waitFor()
await clip.pause(600)

clip.mark('Kolone')
await clip.click(page.getByRole('button', { name: /Proveri/ }), { after: 400 })
await page.getByRole('button', { name: 'Uvezi 15 artikala' }).waitFor()
await clip.pause(700)

clip.mark('Potvrda')
await clip.click(page.getByRole('button', { name: 'Uvezi 15 artikala' }), { after: 300 })
await page.getByRole('link', { name: 'Idi na artikle' }).waitFor()
await clip.caption('Uvezeno: 15 artikala sa stanjem i minimumom.', CAPTION_X)
await clip.pause(700)
await clip.click(page.getByRole('link', { name: 'Idi na artikle' }), { after: 200 })
await page.getByText('57', { exact: true }).first().waitFor()
await clip.moveTo(860, 430)
await clip.pause(1300)

const timeline = await clip.stop()
printResult(
  await renderVertical({
    dir,
    name: 'csv-uvoz',
    timeline,
    scenes: await scenesFromMarks(timeline),
    frame: { title: ['Imaš lager', 'u Excelu?'], message: ['Uvezi ga za minut,', 'i kreni od prvog dana.'] },
  }),
)
