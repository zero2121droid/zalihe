// Downloads the OpenAPI document from the running API (dotnet run ... --launch-profile http)
// and saves it formatted, so changes to the API are readable in git diffs.
import { writeFile } from 'node:fs/promises'

const url = process.env.OPENAPI_URL ?? 'http://localhost:5131/openapi/v1.json'

let response
try {
  response = await fetch(url)
} catch {
  console.error(`Cannot reach ${url}. Start the API first: dotnet run --project src/Zalihe.Web --launch-profile http`)
  process.exit(1)
}
if (!response.ok) {
  console.error(`${url} returned ${response.status}`)
  process.exit(1)
}

const document = await response.json()
await writeFile(new URL('../openapi.json', import.meta.url), JSON.stringify(document, null, 2) + '\n')
console.log(`Saved openapi.json from ${url}`)
