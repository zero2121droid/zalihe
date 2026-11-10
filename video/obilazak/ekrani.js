// Screenshots for the feature tour, at 1920x1080 and without captions. Run while the test shop
// shows 4 pcs of the white T-shirt M, i.e. after porudzbina/snimi.js (the app pushes it after 30 s).
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { seed } from '../seed-demo.js'
import { startRecording, WIDE } from '../lib/record.js'

const dir = join(dirname(fileURLToPath(import.meta.url)), 'ekrani')
const ITEM = 'Majica Basic, bela – M'
seed('full')

const clip = await startRecording({ dir: join(dir, '.raw'), path: '/', record: false, viewport: WIDE })
const { page } = clip
const settle = async () => {
  await page.waitForLoadState('networkidle')
  await page.evaluate(() => document.getElementById('video-cursor')?.remove())
  await page.mouse.move(1, 1)
  await page.waitForTimeout(1200)
}
const shot = async (name) => {
  await settle()
  await page.screenshot({ path: join(dir, `${name}.png`) })
}

await shot('1-pocetna')
await clip.goto('/items')
await shot('2-artikli')

await page.getByLabel('Pretraga artikala').fill('BELA-M')
await page.getByRole('link', { name: new RegExp(ITEM) }).first().waitFor()
await page.waitForTimeout(600)
await page.getByRole('link', { name: new RegExp(ITEM) }).first().click()
await page.getByRole('heading', { name: 'Istorija' }).waitFor()
await shot('3-artikal')
await page.getByRole('heading', { name: 'Istorija' }).scrollIntoViewIfNeeded()
await page.evaluate(() => {
  const top = [...document.querySelectorAll('h2')].find((h) => h.textContent === 'Istorija').getBoundingClientRect().top
  document.scrollingElement.scrollTop += top - 40
})
await shot('8-istorija')

await clip.goto('/orders')
await shot('4-porudzbine')

await clip.goto('/receipts/new')
await page.getByLabel('Dobavljač').fill('Tekstil Niš')
await page.keyboard.press('Escape')
await page.getByLabel('Broj fakture ili otpremnice').fill('119')
await page.getByPlaceholder('Naziv, šifra ili barkod').fill('BELA-M')
await page.getByRole('option', { name: new RegExp(ITEM) }).click()
await page.getByLabel(`Količina za ${ITEM}`).fill('24')
await page.getByLabel(`Nabavna cena za ${ITEM}`).fill('850')
await page.getByPlaceholder('Naziv, šifra ili barkod').fill('BELA-L')
await page.getByRole('option', { name: /Majica Basic, bela – L/ }).click()
await page.getByLabel('Količina za Majica Basic, bela – L').fill('18')
await page.getByLabel('Nabavna cena za Majica Basic, bela – L').fill('850')
await page.getByPlaceholder('Naziv, šifra ili barkod').fill('BELA-S')
await page.getByRole('option', { name: /Majica Basic, bela – S/ }).click()
await page.getByLabel('Količina za Majica Basic, bela – S').fill('12')
await page.getByLabel('Nabavna cena za Majica Basic, bela – S').fill('850')
await page.getByText('Novi prijem').first().click()
await shot('6-prijem')

await clip.goto('/reports')
await shot('7-izvestaji')

await page.goto('http://localhost:8080/?p=208')
await page.locator('select').first().selectOption('M')
await page.getByText(/na zalihama/i).first().waitFor()
console.log(await page.getByText(/na zalihama/i).first().textContent())
await settle()
await page.screenshot({ path: join(dir, '5-sajt.png') })
await clip.stop()
