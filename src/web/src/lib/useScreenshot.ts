import { useState } from 'react'
import { MAX_SCREENSHOT_SIDE, shrinkImage, UnreadableImageError } from './shrinkImage'

export interface Screenshot {
  /** The shrunk picture, ready to send; null without one. */
  value: Blob | null
  /** A picked picture is still being shrunk; the form waits for it. */
  shrinking: boolean
  /** The picked file could not be opened as a picture. */
  unreadable: boolean
  /** Shrinks and keeps a picked or pasted file. */
  pick: (file: File | null | undefined) => Promise<void>
  clear: () => void
}

/**
 * The optional picture of the screen for a message (SPEC 11.10). The user picks or pastes it, so they
 * see exactly what is sent. It is shrunk to MAX_SCREENSHOT_SIDE in the browser, large enough to read.
 */
export function useScreenshot(): Screenshot {
  const [value, setValue] = useState<Blob | null>(null)
  const [shrinking, setShrinking] = useState(false)
  const [unreadable, setUnreadable] = useState(false)

  async function pick(file: File | null | undefined) {
    if (!file) return
    setUnreadable(false)
    setShrinking(true)
    try {
      setValue(await shrinkImage(file, MAX_SCREENSHOT_SIDE))
    } catch (e) {
      if (!(e instanceof UnreadableImageError)) throw e
      setUnreadable(true)
    } finally {
      setShrinking(false)
    }
  }

  function clear() {
    setValue(null)
    setUnreadable(false)
  }

  return { value, shrinking, unreadable, pick, clear }
}
