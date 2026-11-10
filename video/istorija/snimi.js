// Clip "Istorija": today's stock, and every change behind it, on the phone.
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { seed } from '../seed-demo.js'
import { PHONE, startRecording } from '../lib/record.js'
import { printResult, renderVertical, scenesFromMarks } from '../lib/render.js'

const dir = dirname(fileURLToPath(import.meta.url))
seed('full')

const clip = await startRecording({ dir: join(dir, 'raw'), path: '/items', viewport: PHONE })
const { page } = clip
const top = (locator) => locator.evaluate((element) => element.getBoundingClientRect().top + scrollY)

clip.mark('Pretraga')
await clip.type(page.getByPlaceholder('Naziv, šifra ili barkod').first(), 'BELA-M', { after: 600 })
await clip.click(page.getByRole('link', { name: 'Otvori artikal Majica Basic, bela – M' }), { after: 200 })
const history = page.getByRole('heading', { name: 'Istorija' })
await history.waitFor()

clip.mark('Stanje 28 kom')
await clip.scrollTo((await top(page.getByText('Stanje', { exact: true }).first())) - 150, 700)
await clip.caption('Sada ima 28 komada.')
await clip.pause(1600)

clip.mark('Stanje kroz vreme')
await clip.caption('A svaka promena ostaje zapisana:\nko, kada i zašto.')
await clip.scrollTo((await top(page.getByText('Stanje kroz vreme'))) - 70, 800)
await clip.pause(1000)

clip.mark('Istorija')
await clip.scrollTo((await top(history)) - 70, 800)
await clip.pause(700)
const filters = page.getByLabel('Filter istorije')
await clip.click(filters.getByRole('button', { name: 'Količina' }), { after: 700 })
await clip.click(filters.getByRole('button', { name: 'Sve', exact: true }), { after: 900 })

const timeline = await clip.stop()
const scenes = await scenesFromMarks(timeline, { Pretraga: { speed: 1.8 } })
printResult(
  await renderVertical({
    dir,
    name: 'istorija',
    timeline,
    scenes,
    phone: true,
    frame: { title: ['Ko je šta', 'promenio?'], message: ['Sve ostaje zapisano:', 'ko, kada i zašto.'] },
  }),
)
