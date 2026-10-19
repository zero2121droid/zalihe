/** The longest side of a picture after shrinking: sharp on the item page, small enough for the database. */
export const MAX_IMAGE_SIDE = 800

const QUALITY = 0.85

/** The picture could not be opened (not a picture, or a format this browser can't read, e.g. HEIC in Chrome). */
export class UnreadableImageError extends Error {}

/** The size that fits within `max` on both sides, keeping the proportions; never enlarges. */
export function fitWithin(width: number, height: number, max = MAX_IMAGE_SIDE): { width: number; height: number } {
  const scale = Math.min(1, max / Math.max(width, height))
  return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) }
}

/**
 * Shrinks a picture the user picked before it is sent, so the API never needs an image library:
 * at most MAX_IMAGE_SIDE px, on a white background (product photos; no transparency), as WebP or,
 * in a browser that can't write WebP, JPEG. Phone photos are turned upright on the way.
 */
export async function shrinkImage(file: Blob): Promise<Blob> {
  let bitmap: ImageBitmap
  try {
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  } catch {
    throw new UnreadableImageError()
  }

  const { width, height } = fitWithin(bitmap.width, bitmap.height)
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const context = canvas.getContext('2d')
  if (!context) throw new UnreadableImageError()
  context.fillStyle = 'white'
  context.fillRect(0, 0, width, height)
  context.drawImage(bitmap, 0, 0, width, height)
  bitmap.close()

  const webp = await toBlob(canvas, 'image/webp')
  // A browser that can't write a type quietly writes PNG instead.
  if (webp?.type === 'image/webp') return webp
  const jpeg = await toBlob(canvas, 'image/jpeg')
  if (!jpeg) throw new UnreadableImageError()
  return jpeg
}

function toBlob(canvas: HTMLCanvasElement, type: string) {
  return new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, type, QUALITY))
}
