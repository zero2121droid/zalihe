/**
 * A short name for a browser's User-Agent, e.g. "Chrome 131, macOS", so the user sees what goes with a
 * message and the platform's owner can read it at a glance. Unknown parts are left out.
 */
export function describeClient(userAgent: string): string {
  return [browser(userAgent), system(userAgent)].filter(Boolean).join(', ')
}

function browser(ua: string): string | null {
  // Order matters: Edge and Opera also say "Chrome", and Chrome also says "Safari".
  const rules: [RegExp, string][] = [
    [/Edg(?:e|A|iOS)?\/(\d+)/, 'Edge'],
    [/(?:OPR|Opera)\/(\d+)/, 'Opera'],
    [/SamsungBrowser\/(\d+)/, 'Samsung Internet'],
    [/(?:Firefox|FxiOS)\/(\d+)/, 'Firefox'],
    [/(?:Chrome|CriOS)\/(\d+)/, 'Chrome'],
    [/Version\/(\d+)[\d.]* .*Safari\//, 'Safari'],
  ]
  for (const [pattern, name] of rules) {
    const match = pattern.exec(ua)
    if (match) return `${name} ${match[1]}`
  }
  return null
}

function system(ua: string): string | null {
  if (/iPhone|iPad|iPod/.test(ua)) return 'iOS'
  if (/Android/.test(ua)) return 'Android'
  if (/Windows/.test(ua)) return 'Windows'
  if (/Mac OS X|Macintosh/.test(ua)) return 'macOS'
  if (/Linux/.test(ua)) return 'Linux'
  return null
}
