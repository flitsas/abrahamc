import { defineConfig } from 'vitest/config'
import { resolve } from 'path'

export default defineConfig({
  test: {
    globals: true,
    environment: 'node',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov', 'html'],
      thresholds: { lines: 80, functions: 80, branches: 70 },
      exclude: [
        '**/node_modules/**',
        '**/dist/**',
        '**/tests/**',
        '**/migrations/**',
        '**/infrastructure/**',
        '**/interfaces/**',
        'src/shared/**',
        'src/app.ts',
        'src/server.ts',
        'eslint.config.js',
        'vitest.config.ts',
      ],
    },
  },
  resolve: {
    alias: { '@': resolve(__dirname, './src') },
  },
})
