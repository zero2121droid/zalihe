/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rolldownOptions: {
      output: {
        // Libraries change far less often than the app, so they get files of their own that browsers
        // keep across deployments; an app update then downloads only the app's code.
        codeSplitting: {
          groups: [
            { name: 'react', test: /node_modules[\\/](react|react-dom|react-router|scheduler|@tanstack)[\\/]/ },
            { name: 'mantine', test: /node_modules[\\/](@mantine|@floating-ui)[\\/]/ },
          ],
        },
      },
    },
  },
  server: {
    proxy: {
      // Backend runs outside Docker via the "http" launch profile of Zalihe.Web.
      // API_URL points the proxy elsewhere, e.g. a second backend on another port.
      '/api': process.env.API_URL ?? 'http://localhost:5131',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
