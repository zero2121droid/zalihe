import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { COLOR_SCHEME_KEY } from '../colorScheme'
import { renderPage } from '../test/render'
import { ThemeToggle } from './ThemeToggle'

const scheme = () => document.documentElement.getAttribute('data-mantine-color-scheme')

describe('ThemeToggle', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('Render_NothingSaved_StartsDark', async () => {
    // Act
    await renderPage(<ThemeToggle />)

    // Assert
    expect(scheme()).toBe('dark')
    expect(screen.getByRole('button', { name: 'Uključi svetlu temu' })).toBeInTheDocument()
  })

  it('Click_InDarkTheme_SwitchesToLightAndRemembersIt', async () => {
    // Arrange
    await renderPage(<ThemeToggle />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Uključi svetlu temu' }))

    // Assert
    expect(scheme()).toBe('light')
    expect(localStorage.getItem(COLOR_SCHEME_KEY)).toBe('light')
    expect(screen.getByRole('button', { name: 'Uključi tamnu temu' })).toBeInTheDocument()
  })

  it('Render_LightSaved_StartsLight', async () => {
    // Arrange
    localStorage.setItem(COLOR_SCHEME_KEY, 'light')

    // Act
    await renderPage(<ThemeToggle />)

    // Assert
    expect(scheme()).toBe('light')
  })
})
