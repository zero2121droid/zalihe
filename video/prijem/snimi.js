// Clip "Prijem robe": one receipt from the invoice, all lines in at once, entered on the phone.
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { seed } from '../seed-demo.js'
import { PHONE, startRecording } from '../lib/record.js'
import { printResult, renderVertical, scenesFromMarks } from '../lib/render.js'

const dir = dirname(fileURLToPath(import.meta.url))
const ITEM = 'Majica Basic, bela – M'
seed('order')

const clip = await startRecording({ dir: join(dir, 'raw'), path: '/receipts', viewport: PHONE })
const { page } = clip

clip.mark('Novi prijem')
await clip.caption('Stigne nova roba.\nPrijem se upisuje jednom, sa fakture.')
await clip.pause(600)
await clip.click(page.getByRole('link', { name: 'Novi prijem' }).or(page.getByRole('button', { name: 'Novi prijem' })), { after: 500 })

clip.mark('Faktura')
await clip.type(page.getByLabel('Dobavljač'), 'Tekstil Niš', { after: 250 })
await page.keyboard.press('Escape')
await clip.type(page.getByLabel('Broj fakture ili otpremnice'), '118', { after: 300 })

clip.mark('Stavka')
await clip.scrollTo(150, 500)
await clip.type(page.getByPlaceholder('Naziv, šifra ili barkod'), 'BELA-M', { after: 450 })
await clip.click(page.getByRole('option', { name: new RegExp(ITEM) }), { after: 400 })
await clip.scrollTo(330, 600)
await clip.replace(page.getByLabel(`Količina za ${ITEM}`), '24', { after: 250 })
await clip.replace(page.getByLabel(`Nabavna cena za ${ITEM}`), '850', { after: 400 })

clip.mark('Proknjiženo')
// The caption would cover the button at the bottom of the form while it is tapped.
await clip.caption('')
await clip.scrollTo(10_000, 600)
await clip.click(page.getByRole('button', { name: 'Proknjiži prijem' }), { after: 700 })
await page.getByText('Tekstil Niš').first().waitFor()
await clip.caption('Prijem je proknjižen:\n+24 komada, faktura 118.')
if (!(await page.getByRole('dialog').isVisible())) {
  await clip.click(page.getByText('118').first(), { after: 300 })
}
await clip.pause(2000)

const timeline = await clip.stop()
const fast = { speed: 1.6 }
const scenes = await scenesFromMarks(timeline, { Faktura: fast, Stavka: fast })
printResult(
  await renderVertical({
    dir,
    name: 'prijem',
    timeline,
    scenes,
    phone: true,
    frame: { title: ['Stigla je', 'nova roba?'], message: ['Jedan prijem, sa fakture.', 'Sve stavke ulaze odjednom.'] },
  }),
)
