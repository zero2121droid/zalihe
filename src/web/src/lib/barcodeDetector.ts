import wasmUrl from 'zxing-wasm/reader/zxing_reader.wasm?url'

/** Codes on goods and labels: EAN and UPC from manufacturers, Code 128 and 39 on in-house labels, QR. */
const FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'code_39', 'qr_code']

export interface Detector {
  detect(source: HTMLVideoElement): Promise<Array<{ rawValue: string }>>
}

type NativeDetector = (new (options: { formats: string[] }) => Detector) & { getSupportedFormats(): Promise<string[]> }

/**
 * A barcode reader for the camera picture. The browser's own (Chrome on Android, macOS) when it reads
 * EAN-13; otherwise ZXing in WebAssembly, loaded only now and served from this app, never a CDN.
 */
export async function createDetector(): Promise<Detector> {
  const native = (window as unknown as { BarcodeDetector?: NativeDetector }).BarcodeDetector
  if (native && (await native.getSupportedFormats()).includes('ean_13')) {
    return new native({ formats: FORMATS })
  }

  const { BarcodeDetector, prepareZXingModule } = await import('barcode-detector/ponyfill')
  prepareZXingModule({
    overrides: { locateFile: (path: string, prefix: string) => (path.endsWith('.wasm') ? wasmUrl : prefix + path) },
  })
  return new BarcodeDetector({ formats: FORMATS as never })
}

/** Whether this browser can open a camera at all (it needs a secure page: https or localhost). */
export const canUseCamera = () => typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getUserMedia
