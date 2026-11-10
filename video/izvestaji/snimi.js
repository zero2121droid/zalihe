// Clip "Izveštaji": what sells, what sits on the shelf, and how much it is worth. See NAPOMENE.md.
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { seed } from '../seed-demo.js'
import { startRecording } from '../lib/record.js'
import { printResult, renderVertical, scenesFromMarks } from '../lib/render.js'

const dir = dirname(fileURLToPath(import.meta.url))
seed('base')

const clip = await startRecording({ dir: join(dir, 'raw') })
const { page } = clip
const tab = (name) => page.getByRole('button', { name, exact: true })

clip.mark('Otvaranje izveštaja')
await clip.caption('Šta se prodaje, a šta stoji na polici?')
await clip.pause(500)
await clip.click(page.getByRole('link', { name: 'Izveštaji' }), { after: 400 })
await page.getByText('Najprodavaniji u poslednjih 30 dana').waitFor()

clip.mark('Najprodavaniji')
await clip.moveTo(600, 420)
await clip.pause(1900)

clip.mark('Roba koja stoji')
await clip.caption('Vidiš koliko je novca vezano u robi koja stoji.')
await clip.click(tab('Roba koja stoji'), { after: 2100 })

clip.mark('Vrednost po kategoriji')
await clip.caption('Sve iz porudžbina i prijema, bez ručnih tabela.')
await clip.click(tab('Vrednost po kategoriji'), { after: 2100 })

const timeline = await clip.stop()
const scenes = await scenesFromMarks(timeline)
const result = await renderVertical({
  dir,
  name: 'izvestaji',
  timeline,
  scenes,
  frame: { title: ['Šta ti se', 'najbolje prodaje?'], message: ['Izveštaji iz tvog lagera,', 'bez Excel tabela.'] },
})
printResult(result)
