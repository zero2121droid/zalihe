import { Box } from '@mantine/core'
import { IconPhoto } from '@tabler/icons-react'
import { useState } from 'react'

/**
 * The item's picture in a square: the shop's image or one the user added. Without one, or when the
 * image doesn't load (e.g. it was removed from the shop), an empty frame with a quiet icon keeps
 * the rows aligned. Decorative (empty alt): the item's name is always next to it.
 */
export function ItemImage({ url, size }: { url: string | null; size: number }) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  const shown = url !== null && url !== failedUrl

  return (
    <Box
      w={size}
      h={size}
      bg="var(--z-surface-2)"
      bd="1px solid var(--z-line)"
      style={{ borderRadius: 'var(--mantine-radius-sm)', overflow: 'hidden', flexShrink: 0, display: 'grid', placeItems: 'center' }}
    >
      {shown ? (
        <img
          src={url}
          alt=""
          loading="lazy"
          decoding="async"
          referrerPolicy="no-referrer"
          onError={() => setFailedUrl(url)}
          style={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }}
        />
      ) : (
        <IconPhoto size={Math.round(size * 0.4)} stroke={1.8} color="var(--z-muted)" aria-hidden />
      )}
    </Box>
  )
}
