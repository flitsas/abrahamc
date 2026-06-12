import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from 'tailwindcss'
import autoprefixer from 'autoprefixer'

export default defineConfig({
  plugins: [react()],
  css: {
    postcss: {
      plugins: [tailwindcss, autoprefixer],
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    host: true,
    allowedHosts: true,
    proxy: {
      '/api': {
        target: 'http://localhost:3030',
        changeOrigin: true,
      },
      // Swagger UI + OpenAPI JSON viven en core-api (3030), no en el SPA
      '/swagger': {
        target: 'http://localhost:3030',
        changeOrigin: true,
      },
      '/openapi': {
        target: 'http://localhost:3030',
        changeOrigin: true,
      },
    },
  },
})
