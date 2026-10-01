import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderPage } from '../../test/render'
import { ImportPage } from './ImportPage'

const analysis = {
  status: 200,
  body: {
    columns: ['Naziv', 'Šifra', 'JM', 'Stanje'],
    sampleRows: [['Kafa Etiopija', 'KF-ETI-250', 'kom', '12']],
    suggestedMapping: { name: 0, sku: 1, unit: 2, initialStock: 3 },
    rowCount: 3,
  },
}

const preview = {
  status: 200,
  body: {
    readyCount: 1,
    errorCount: 1,
    skippedCount: 1,
    issues: [
      { rowNumber: 3, sku: 'SO-1', name: 'Šolja', skipped: false, errors: [{ code: 'import.unit_unknown', field: 'unit', params: { value: 'kutija' } }] },
      { rowNumber: 4, sku: 'KF-1', name: 'Kafa', skipped: true, errors: [{ code: 'import.sku_exists', field: 'sku' }] },
    ],
    sample: [{ rowNumber: 2, name: 'Kafa Etiopija', sku: 'KF-ETI-250', unit: 'kom', initialStock: 12 }],
  },
}

const csv = () => new File(['Naziv;Šifra;JM;Stanje\nKafa Etiopija;KF-ETI-250;kom;12\n'], 'artikli.csv', { type: 'text/csv' })

/** The select's input; Mantine also labels its dropdown list with the same label. */
const selectInput = (label: string | RegExp) =>
  screen.getAllByLabelText(label).find((el) => el.tagName === 'INPUT') as HTMLInputElement

async function chooseFile() {
  const input = document.querySelector('input[type="file"]') as HTMLInputElement
  await userEvent.upload(input, csv())
}

describe('ImportPage', () => {
  it('Flow_ValidFile_SuggestsColumnsPreviewsAndImports', async () => {
    // Arrange
    const fetchMock = mockApi({
      'POST /api/imports/items/analyze': analysis,
      'POST /api/imports/items/preview': preview,
      'POST /api/imports/items': { status: 200, body: { importedCount: 1, errorCount: 1, skippedCount: 1 } },
      'GET /api/items': { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 20 } },
      'GET /api/stock/summary': { status: 200, body: { belowMinimum: 0, outOfStock: 0 } },
    })
    await renderPage(<ImportPage />, '/items/import')

    // Act: file
    await chooseFile()

    // Assert: suggested mapping is preselected
    expect(await screen.findByText('Fajl ima 3 redova. Prvi redovi:')).toBeInTheDocument()
    expect(selectInput(/^Naziv/)).toHaveValue('A · Naziv')
    expect(selectInput(/^Šifra/)).toHaveValue('B · Šifra')
    expect(selectInput('Barkod')).toHaveValue('Ne uvozi')

    // Act: check
    await userEvent.click(screen.getByRole('button', { name: 'Proveri' }))

    // Assert: preview with translated issues
    expect(await screen.findByText('Jedinica mere: Nepoznata jedinica mere „kutija”.')).toBeInTheDocument()
    expect(screen.getByText('Artikal sa ovom šifrom već postoji, red je preskočen.')).toBeInTheDocument()
    const previewCall = fetchMock.mock.calls.find(([url]) => url === '/api/imports/items/preview')!
    const sent = previewCall[1]!.body as FormData
    expect(sent.get('NameColumn')).toBe('0')
    expect(sent.get('InitialStockColumn')).toBe('3')
    expect(sent.get('BarcodeColumn')).toBeNull()
    expect((sent.get('File') as File).name).toBe('artikli.csv')

    // Act: import
    await userEvent.click(screen.getByRole('button', { name: 'Uvezi 1 artikal' }))

    // Assert
    expect(await screen.findByText('Uvezen je 1 artikal')).toBeInTheDocument()
    expect(screen.getByText('Preskočeno redova: 2.')).toBeInTheDocument()
  })

  it('ChooseFile_ExcelFile_ShowsHelpfulError', async () => {
    // Arrange
    mockApi({
      'POST /api/imports/items/analyze': {
        status: 400,
        body: { status: 400, code: 'validation.failed', errors: [{ code: 'import.excel_not_supported', field: 'file' }] },
      },
    })
    await renderPage(<ImportPage />, '/items/import')

    // Act
    await chooseFile()

    // Assert
    expect(await screen.findByText(/Excel fajlovi još nisu podržani/)).toBeInTheDocument()
  })

  it('Check_RequiredColumnRemoved_DisablesCheck', async () => {
    // Arrange
    mockApi({ 'POST /api/imports/items/analyze': { ...analysis, body: { ...analysis.body, suggestedMapping: { name: 0 } } } })
    await renderPage(<ImportPage />, '/items/import')

    // Act
    await chooseFile()

    // Assert
    await waitFor(() => expect(screen.getByRole('button', { name: 'Proveri' })).toBeDisabled())
  })
})
