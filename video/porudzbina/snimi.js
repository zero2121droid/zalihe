// Clip "Porudžbina sa Instagrama": an order that never reached the shop, written down on the phone.
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { seed } from '../seed-demo.js'
import { PHONE, startRecording } from '../lib/record.js'
import { printResult, renderVertical, scenesFromMarks } from '../lib/render.js'

const dir = dirname(fileURLToPath(import.meta.url))
seed('base')

const clip = await startRecording({ dir: join(dir, 'raw'), path: '/orders', viewport: PHONE })
const { page } = clip
const modal = page.getByRole('dialog')

clip.mark('Nova porudžbina')
await clip.caption('Na Instagramu neko kupi 4 majice.\nTo upišeš kao ručnu porudžbinu.')
await clip.pause(1100)
await clip.click(page.getByRole('button', { name: 'Nova porudžbina' }), { after: 500 })

clip.mark('Izbor artikla')
await clip.type(modal.getByPlaceholder('Naziv, šifra ili barkod'), 'BELA-M', { after: 500 })
await clip.click(page.getByRole('option', { name: /Majica Basic, bela – M/ }), { after: 500 })

clip.mark('Količina i referenca')
const increase = modal.getByRole('button', { name: 'Povećaj za 1' })
for (let count = 0; count < 3; count++) await clip.click(increase, { after: 180 })
await clip.pause(400)
await clip.type(modal.getByLabel('Referenca'), 'IG 154', { after: 500 })

clip.mark('Sačuvano')
await clip.click(modal.getByRole('button', { name: 'Sačuvaj porudžbinu' }), { after: 200 })
await modal.waitFor({ state: 'hidden' })
await clip.caption('Porudžbina je upisana,\nbez otvaranja wp-admina.')
await page.getByText('R-0002').first().waitFor()
await clip.pause(2600)

const timeline = await clip.stop()
const fast = { speed: 1.6 }
const scenes = await scenesFromMarks(timeline, { 'Izbor artikla': fast, 'Količina i referenca': fast })
printResult(
  await renderVertical({
    dir,
    name: 'porudzbina',
    timeline,
    scenes,
    phone: true,
    frame: { title: ['Porudžbina sa', 'Instagrama?'], message: ['Upiši je za par sekundi.', 'Stanje se skine i na sajtu.'] },
  }),
)
