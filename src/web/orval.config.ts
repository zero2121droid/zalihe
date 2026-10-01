import { defineConfig } from 'orval'

// Generates typed fetch functions and TanStack Query hooks from the API's OpenAPI document.
// openapi.json is committed, so API changes show up in diffs. Refresh it with `npm run api`.
export default defineConfig({
  zalihe: {
    input: { target: './openapi.json' },
    output: {
      mode: 'tags-split',
      target: './src/api/generated',
      schemas: './src/api/generated/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      override: {
        mutator: { path: './src/api/http.ts', name: 'customFetch' },
        // Hooks return the response body; errors are thrown as ApiError by the mutator.
        fetch: { includeHttpResponseReturnType: false },
      },
    },
  },
})
