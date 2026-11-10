// Captions live in the page while recording (STIL.md "Natpisi u snimku"), not in ffmpeg afterwards.
// `centerX` is the caption's horizontal center in the 1920 px page; vertical clips crop the page, so
// a caption must sit in the middle of what stays visible.

export const CENTER = { full: 960, cropped: 772, left: 540, phone: 195 }

const install = () => {
  if (window.__caption) return
  let box
  window.__caption = (text, centerX, bottom) => {
    // A phone window is recorded at 2x, so its caption is half as large in CSS pixels.
    const phone = innerWidth < 600
    if (!box) {
      box = document.createElement('div')
      box.id = 'video-caption'
      box.style.cssText = `position:fixed;z-index:2147483646;transform:translateX(-50%);width:max-content;max-width:1040px;
        box-sizing:border-box;padding:14px 28px;background:rgba(25,28,21,.92);border:1px solid #2C3125;border-radius:8px;
        color:#E4E7DC;font:600 30px/1.3 'IBM Plex Sans',sans-serif;text-align:center;white-space:pre-line;
        pointer-events:none;transition:opacity .25s ease`
      document.documentElement.append(box)
    }
    if (phone) Object.assign(box.style, { fontSize: '15px', padding: '7px 12px', borderRadius: '6px', maxWidth: '360px' })
    box.style.left = `${centerX}px`
    box.style.bottom = `${bottom}px`
    box.style.opacity = text ? '1' : '0'
    if (text) box.textContent = text
  }
}

export const captionScript = `(${install.toString()})()`

export async function showCaption(page, text, centerX, bottom) {
  const phone = page.viewportSize().width < 600
  await page.evaluate(([value, x, y]) => window.__caption(value, x, y), [text, centerX ?? (phone ? CENTER.phone : CENTER.cropped), bottom ?? (phone ? 84 : 40)])
}
