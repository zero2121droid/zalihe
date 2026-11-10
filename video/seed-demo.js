// Creates the demo company "Nit" for the promo clips in the local database. Only ever touches its
// own tenant; other tenants are read for the shop's products, photos and API key.
//
//   node video/seed-demo.js --stage=base    Majica Basic, bela – M at 8 pcs
//   node video/seed-demo.js --stage=order   4 pcs, order R-0002 (IG 154) recorded
//   node video/seed-demo.js --stage=full    28 pcs, receipt 118 and Marko's change recorded
//
// Rows have the same shape the application writes (OrderMovements, ReceiptService, ManualOrderService),
// so nothing looks different on screen. History goes straight to SQL because the API can't backdate it.
import { execFileSync } from 'node:child_process'
import { pbkdf2Sync, randomBytes, randomUUID } from 'node:crypto'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
export const TENANT = '0190aaaa-0000-7000-8000-000000000001'
export const DEMO_LOGIN = { email: 'ana@nit.test', password: 'Lagerko-demo-1' }
const ANA = '0190aaaa-0000-7000-8000-000000000002'
const MARKO = '0190aaaa-0000-7000-8000-000000000003'
const CHANNEL = '0190aaaa-0000-7000-8000-000000000004'
export const MAIN_SKU = 'NIT-MAJ-BELA-M'
const DAY = 86_400_000
const HISTORY_DAYS = 96

// Stock of the items the clips don't follow closely; everything else is random but repeatable.
const MINIMUMS = { 'NIT-MAJ-BELA-S': 5, 'NIT-MAJ-BELA-L': 5, 'NIT-DUK-PLAVI-M': 4, 'NIT-FAR-SLIM-32': 3, 'NIT-CAR-SET3': 10 }
const FINAL_STOCK = { 'NIT-MAJ-BELA-S': 3, 'NIT-MAJ-BELA-L': 11, 'NIT-DUK-PLAVI-M': 2, 'NIT-FAR-SLIM-32': 0, 'NIT-CAR-SET3': 24 }
// Dead stock for the "Roba koja stoji" report: on the shelf, not sold for over 60 days.
const SLOW = /^NIT-(JAK-TREN|JAK-KOZ|DZE-PRUG|FAR-REG)/

const psql = (input, ...flags) =>
  execFileSync('docker', ['compose', 'exec', '-T', 'postgres', 'psql', '-U', 'zalihe', '-d', 'zalihe', '-v', 'ON_ERROR_STOP=1', ...flags], {
    cwd: root,
    input,
    encoding: 'utf8',
    maxBuffer: 1 << 28,
  })

const lit = (value) => {
  if (value === null || value === undefined) return 'null'
  if (typeof value === 'number') return String(value)
  if (typeof value === 'boolean') return String(value)
  if (value instanceof Date) return `'${value.toISOString()}'`
  return `'${String(value).replaceAll("'", "''")}'`
}

const insert = (table, rows) => {
  if (rows.length === 0) return ''
  const columns = Object.keys(rows[0])
  const values = rows.map((row) => `(${columns.map((c) => lit(row[c])).join(',')})`)
  return `insert into "${table}" (${columns.map((c) => `"${c}"`).join(',')}) values\n${values.join(',\n')};\n`
}

/** ASP.NET Identity v3 hash: PBKDF2-HMAC-SHA512, 100,000 iterations. */
function identityHash(password) {
  const salt = randomBytes(16)
  const header = Buffer.alloc(13)
  header.writeUInt8(1, 0)
  header.writeUInt32BE(2, 1)
  header.writeUInt32BE(100_000, 5)
  header.writeUInt32BE(salt.length, 9)
  return Buffer.concat([header, salt, pbkdf2Sync(password, salt, 100_000, 32, 'sha512')]).toString('base64')
}

/** Small seeded generator, so every run gives the same shop. */
function mulberry32(seed) {
  return () => {
    seed = (seed + 0x6d2b79f5) | 0
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

function readCatalog() {
  const json = psql(
    `select json_agg(c) from (
       select i."Sku" sku, i."Name" name, i."Unit" unit, i."Category" category, i."GroupName" "group",
              i."PurchasePrice" purchase, i."SalePrice" sale, m."ExternalId" external, m."ParentExternalId" parent, m."ImageUrl" image
       from "Items" i join "ItemChannelMappings" m on m."ItemId" = i."Id"
       join "Tenants" t on t."Id" = i."TenantId"
       where t."Name" = 'Nit' and t."Id" <> '${TENANT}' and i."Sku" like 'NIT-%' and i."IsActive"
       order by i."Sku") c`,
    '-At',
  )
  const catalog = JSON.parse(json || 'null')
  if (!catalog?.some((item) => item.sku === MAIN_SKU)) {
    throw new Error('Local company "Nit" with the demo shop products is missing. Run docker/woocommerce/demo.sh and connect the shop first.')
  }
  return catalog
}

function readChannelSecrets() {
  const row = psql(
    `select c."EncryptedCredentials" || '|' || c."EncryptedWebhookSecret" from "SalesChannels" c
     join "Tenants" t on t."Id" = c."TenantId"
     where t."Name" = 'Nit' and t."Id" <> '${TENANT}' and c."BaseUrl" = 'http://localhost:8080' limit 1`,
    '-At',
  ).trim()
  if (!row) throw new Error('Local company "Nit" has no channel for http://localhost:8080.')
  const [credentials, webhookSecret] = row.split('|')
  return { credentials, webhookSecret }
}

function buildShop(stage, now) {
  const random = mulberry32(20261106)
  const pick = (from, to) => from + Math.floor(random() * (to - from + 1))
  const start = new Date(now - HISTORY_DAYS * DAY)
  const catalog = readCatalog()
  const channel = readChannelSecrets()

  const items = catalog.map((source, index) => {
    const main = source.sku === MAIN_SKU
    const sale = Number(source.sale ?? 2990)
    const purchase = main ? 780 : Number(source.purchase ?? Math.round((sale * 0.46) / 10) * 10)
    const slow = SLOW.test(source.sku)
    return {
      ...source,
      id: randomUUID(),
      purchase,
      saleprice: sale,
      main,
      slow,
      min: main ? 5 : (MINIMUMS[source.sku] ?? (index % 3 === 0 ? 3 : 2)),
      final: main ? 8 : (FINAL_STOCK[source.sku] ?? pick(5, 26)),
      // How well it sells; the white T-shirts and socks lead the report.
      weight: main ? 26 : /MAJ-BELA|CAR-SET/.test(source.sku) ? 7 : slow ? 1 : 1 + random() * 4,
    }
  })

  const movements = []
  const orders = []
  const lines = []
  const move = (item, type, quantity, occurredAt, source, extra = {}) =>
    movements.push({
      Id: randomUUID(),
      TenantId: TENANT,
      ItemId: item.id,
      Type: type,
      Quantity: quantity,
      OccurredAt: occurredAt,
      Source: source,
      ExternalRef: extra.ref ?? null,
      Note: extra.note ?? null,
      UserId: extra.user ?? null,
    })

  // Sales, day by day: mostly shop orders, some sold in the store and booked by hand.
  const sold = new Map(items.map((item) => [item.id, 0]))
  const totalWeight = items.reduce((sum, item) => sum + item.weight, 0)
  const drawItem = (daysAgo) => {
    for (;;) {
      let left = random() * totalWeight
      const item = items.find((candidate) => (left -= candidate.weight) <= 0) ?? items[0]
      if (item.slow && daysAgo < 64) continue
      return item
    }
  }
  let shopOrderNumber = 1012
  for (let daysAgo = HISTORY_DAYS - 6; daysAgo >= 1; daysAgo--) {
    const dayStart = now - daysAgo * DAY
    // In order of time, so shop order numbers grow with the date, as in a real shop.
    const seconds = Array.from({ length: pick(1, 4) }, () => pick(8, 19) * 3_600 + pick(0, 3_500)).sort((a, b) => a - b)
    for (const second of seconds) {
      const placedAt = new Date(dayStart - (dayStart % DAY) + second * 1000)
      const chosen = [...new Set(Array.from({ length: pick(1, 2) }, () => drawItem(daysAgo)))]
      if (random() < 0.72) {
        const order = {
          Id: randomUUID(),
          TenantId: TENANT,
          ChannelId: CHANNEL,
          ExternalId: String(shopOrderNumber++),
          Status: daysAgo < 3 ? 'processing' : 'completed',
          LastModifiedAt: placedAt,
          CreatedAt: placedAt,
          PlacedAt: placedAt,
          Note: null,
          Reference: null,
          Source: 'WooCommerce',
        }
        orders.push(order)
        for (const item of chosen) {
          const quantity = pick(1, 2)
          sold.set(item.id, sold.get(item.id) + quantity)
          lines.push({
            Id: randomUUID(),
            TenantId: TENANT,
            ExternalOrderId: order.Id,
            ExternalProductId: item.external,
            ItemId: item.id,
            OrderedQuantity: quantity,
            AppliedQuantity: quantity,
            Name: item.name,
          })
          move(item, 'Sale', -quantity, placedAt, 'WooCommerce', { ref: order.ExternalId })
        }
      } else {
        const item = chosen[0]
        const quantity = pick(1, 2)
        sold.set(item.id, sold.get(item.id) + quantity)
        move(item, 'Sale', -quantity, placedAt, 'Manual', { note: 'Prodato u lokalu', user: ANA })
      }
    }
  }

  // Opening stock plus restocks, so that history ends exactly on each item's final stock.
  const receipts = []
  const receiptLines = []
  const openedAt = new Date(start.getTime())
  for (const item of items) {
    const restock = item.slow ? 0 : Math.ceil(sold.get(item.id) * 0.5)
    const opening = item.final + sold.get(item.id) - restock
    move(item, 'Adjustment', opening, openedAt, 'WooCommerce', { note: 'Početno stanje' })
    item.restock = restock
  }
  const supplierDates = [52, 19]
  const suppliers = ['Tekstil Niš', 'Pletenina Čačak']
  supplierDates.forEach((daysAgo, index) => {
    const postedAt = new Date(now - daysAgo * DAY)
    const number = String(87 + index * 14)
    const receipt = {
      Id: randomUUID(),
      TenantId: TENANT,
      SupplierName: suppliers[index],
      DocumentNumber: number,
      DocumentKey: `${suppliers[index].toLowerCase()}|${number}`,
      DocumentDate: postedAt.toISOString().slice(0, 10),
      Status: 'Posted',
      CreatedBy: ANA,
      CreatedAt: postedAt,
      PostedAt: postedAt,
    }
    receipts.push(receipt)
    let position = 0
    for (const item of items) {
      const quantity = index === 0 ? Math.floor(item.restock / 2) : Math.ceil(item.restock / 2)
      if (quantity <= 0) continue
      receiptLines.push({
        Id: randomUUID(),
        TenantId: TENANT,
        ReceiptId: receipt.Id,
        ItemId: item.id,
        Position: position++,
        Quantity: quantity,
        UnitPrice: item.purchase,
      })
      move(item, 'Receipt', quantity, postedAt, 'Manual', { note: receipt.SupplierName, ref: number, user: ANA })
    }
  })

  // One Instagram order from the day before: the clips' own order is then R-0002.
  const instagramItem = items.find((item) => item.sku === 'NIT-CAR-SET3') ?? items[0]
  const addManualOrder = (externalId, reference, item, quantity, placedAt) => {
    const order = {
      Id: randomUUID(),
      TenantId: TENANT,
      ChannelId: null,
      ExternalId: externalId,
      Status: 'confirmed',
      LastModifiedAt: placedAt,
      CreatedAt: placedAt,
      PlacedAt: placedAt,
      Note: null,
      Reference: reference,
      Source: 'Manual',
    }
    orders.push(order)
    lines.push({
      Id: randomUUID(),
      TenantId: TENANT,
      ExternalOrderId: order.Id,
      ExternalProductId: item.id,
      ItemId: item.id,
      OrderedQuantity: quantity,
      AppliedQuantity: quantity,
      Name: item.name,
    })
    move(item, 'Sale', -quantity, placedAt, 'Manual', { ref: externalId, user: ANA })
  }
  addManualOrder('R-0001', 'IG 153', instagramItem, 2, new Date(now - 2 * DAY))
  instagramItem.final -= 2

  const main = items.find((item) => item.main)
  const itemChanges = items.map((item) => ({
    Id: randomUUID(),
    TenantId: TENANT,
    ItemId: item.id,
    Kind: 'Created',
    UserId: ANA,
    OccurredAt: new Date(openedAt.getTime() - 60_000),
    Changes: '[]',
  }))
  let lastManualOrder = 1

  if (stage !== 'base') {
    addManualOrder('R-0002', 'IG 154', main, 4, new Date(now - 6 * 3_600_000))
    main.final -= 4
    lastManualOrder = 2
  }
  if (stage === 'full') {
    const postedAt = new Date(now - 2 * 3_600_000)
    const receipt = {
      Id: randomUUID(),
      TenantId: TENANT,
      SupplierName: 'Tekstil Niš',
      DocumentNumber: '118',
      DocumentKey: 'tekstil niš|118',
      DocumentDate: postedAt.toISOString().slice(0, 10),
      Status: 'Posted',
      CreatedBy: ANA,
      CreatedAt: postedAt,
      PostedAt: postedAt,
    }
    receipts.push(receipt)
    receiptLines.push({ Id: randomUUID(), TenantId: TENANT, ReceiptId: receipt.Id, ItemId: main.id, Position: 0, Quantity: 24, UnitPrice: 850 })
    move(main, 'Receipt', 24, postedAt, 'Manual', { note: 'Tekstil Niš', ref: '118', user: ANA })
    main.final += 24
    itemChanges.push({
      Id: randomUUID(),
      TenantId: TENANT,
      ItemId: main.id,
      Kind: 'Updated',
      UserId: MARKO,
      OccurredAt: new Date(now - 3_600_000),
      Changes: JSON.stringify([{ Field: 'purchasePrice', NewValue: '850', OldValue: '780' }]),
    })
    main.purchase = 850
  }

  return { items, movements, orders, lines, receipts, receiptLines, itemChanges, lastManualOrder, channel, start, now: new Date(now) }
}

function sql({ items, movements, orders, lines, receipts, receiptLines, itemChanges, lastManualOrder, channel, start, now }) {
  const tables = ['ExternalOrderLines', 'ExternalOrders', 'ReceiptLines', 'Receipts', 'ItemChanges', 'ItemChannelMappings', 'ItemImages', 'LowStockAlerts', 'StockLevels', 'StockMovements', 'WebhookEvents', 'Items', 'SalesChannels', 'ShopAuthorizations', 'Invitations']
  const user = (id, name, email, role) => ({
    Id: id,
    TenantId: TENANT,
    Language: 'sr-Latn',
    UserName: email,
    NormalizedUserName: email.toUpperCase(),
    Email: email,
    NormalizedEmail: email.toUpperCase(),
    EmailConfirmed: true,
    PasswordHash: identityHash(DEMO_LOGIN.password),
    SecurityStamp: randomBytes(16).toString('hex').toUpperCase(),
    ConcurrencyStamp: randomUUID(),
    PhoneNumberConfirmed: false,
    TwoFactorEnabled: false,
    LockoutEnabled: true,
    AccessFailedCount: 0,
    Name: name,
    Role: role,
  })
  return [
    'begin;',
    ...tables.map((table) => `delete from "${table}" where "TenantId" = '${TENANT}';`),
    `delete from "AspNetUsers" where "TenantId" = '${TENANT}';`,
    `delete from "Tenants" where "Id" = '${TENANT}';`,
    insert('Tenants', [{ Id: TENANT, Name: 'Nit', CreatedAt: start, EmployeesSeePurchasePrices: false, LastManualOrderNumber: lastManualOrder }]),
    insert('AspNetUsers', [user(ANA, 'Ana', DEMO_LOGIN.email, 'Admin'), user(MARKO, 'Marko', 'marko@nit.test', 'Manager')]),
    insert('SalesChannels', [{
      Id: CHANNEL,
      TenantId: TENANT,
      Type: 'WooCommerce',
      BaseUrl: 'http://localhost:8080',
      EncryptedCredentials: channel.credentials,
      EncryptedWebhookSecret: channel.webhookSecret,
      Status: 'Connected',
      LastSyncedAt: now,
      CreatedAt: start,
    }]),
    insert('Items', items.map((item) => ({
      Id: item.id,
      TenantId: TENANT,
      Name: item.name,
      Sku: item.sku,
      Unit: item.unit,
      Category: item.category ?? 'Aksesoari',
      GroupName: item.group,
      PurchasePrice: item.purchase,
      SalePrice: item.saleprice,
      MinStock: item.min,
      IsActive: true,
      CreatedAt: new Date(start.getTime() - 60_000),
    }))),
    insert('StockLevels', items.map((item) => ({ ItemId: item.id, TenantId: TENANT, Quantity: item.final, UpdatedAt: now }))),
    insert('ItemChannelMappings', items.map((item) => ({
      Id: randomUUID(),
      TenantId: TENANT,
      ItemId: item.id,
      ChannelId: CHANNEL,
      ExternalId: item.external,
      ParentExternalId: item.parent,
      CreatedAt: start,
      PushedStock: item.final,
      ImageUrl: item.image,
    }))),
    insert('StockMovements', movements),
    insert('ExternalOrders', orders),
    insert('ExternalOrderLines', lines),
    insert('Receipts', receipts),
    insert('ReceiptLines', receiptLines),
    insert('ItemChanges', itemChanges).replace(/'(\[.*?\])'/g, "'$1'::jsonb"),
    'commit;',
  ].join('\n')
}

export function seed(stage = 'base') {
  if (!['base', 'order', 'full'].includes(stage)) throw new Error(`Unknown stage "${stage}": base, order or full.`)
  const shop = buildShop(stage, Date.now())
  psql(sql(shop), '-q')
  const main = shop.items.find((item) => item.main)
  console.log(`Nit (${stage}): ${shop.items.length} items, ${shop.movements.length} movements, ${shop.orders.length} orders. ${main.name}: ${main.final} pcs.`)
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  seed(process.argv.find((arg) => arg.startsWith('--stage='))?.slice(8) ?? 'base')
}
