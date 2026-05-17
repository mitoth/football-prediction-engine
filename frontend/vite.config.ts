import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Aspire's JavaScript integration injects PORT; bind to it when present so the
// dashboard endpoint maps correctly. Falls back to Vite's default locally.
export default defineConfig(() => ({
  plugins: [react()],
  server: {
    port: process.env.PORT ? Number(process.env.PORT) : 5173,
    host: true,
  },
}))
