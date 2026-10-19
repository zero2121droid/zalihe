import { fireEvent, render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AppProviders } from '../AppProviders'
import { ItemImage } from './ItemImage'

function renderImage(url: string | null) {
  return render(
    <AppProviders>
      <ItemImage url={url} size={36} />
    </AppProviders>,
  )
}

describe('ItemImage', () => {
  it('Render_ImageThatFailsToLoad_ShowsTheEmptyFrameInstead', () => {
    // Arrange
    const { container } = renderImage('https://zrno.rs/wp-content/uploads/obrisana.jpeg')
    const img = container.querySelector('img')!
    expect(img).toHaveAttribute('src', 'https://zrno.rs/wp-content/uploads/obrisana.jpeg')

    // Act
    fireEvent.error(img)

    // Assert
    expect(container.querySelector('img')).toBeNull()
    expect(container.querySelector('svg')).not.toBeNull()
  })

  it('Render_NoImage_ShowsTheEmptyFrame', () => {
    const { container } = renderImage(null)

    expect(container.querySelector('img')).toBeNull()
    expect(container.querySelector('svg')).not.toBeNull()
  })
})
