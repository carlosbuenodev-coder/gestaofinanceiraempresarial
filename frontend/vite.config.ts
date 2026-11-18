import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { loadEnv, type ProxyOptions } from 'vite'

export default defineConfig(({ command, mode, isPreview }) => {
  const testing = mode === 'testing'
  const { VITE_API_BASE_URL: configuredTarget } = loadEnv(mode, process.cwd(), 'VITE_')
  const testingTarget = 'http://127.0.0.1:5217'

  if (testing && (command !== 'serve' || isPreview)) {
    throw new Error('Use npm.cmd run dev -- --mode testing para iniciar o proxy de Testing.')
  }
  if (testing && configuredTarget !== undefined && configuredTarget !== testingTarget) {
    throw new Error('VITE_API_BASE_URL no modo testing deve apontar para http://127.0.0.1:5217.')
  }

  const testingProxy = { target: testingTarget, changeOrigin: true }
  const proxy: Record<string, ProxyOptions> = testing
    ? {
        '^/auth/usuarios/login(?:[?]|$)': testingProxy,
        '^/usuarios/me(?:[?]|$)': testingProxy,
        '^/lancamentos(?:/|[?]|$)': testingProxy,
        '^/cadastros-financeiros(?:[?]|$)': testingProxy,
        '^/fluxo-caixa(?:/|[?]|$)': testingProxy,
        '^/teste-conexao(?:[?]|$)': testingProxy,
      }
    : { '/lancamentos': { target: configuredTarget || 'http://localhost:5216', changeOrigin: true } }

  return {
    plugins: [react()],
    ...(testing ? { cacheDir: 'node_modules/.vite-testing' } : {}),
    server: {
      host: 'localhost',
      port: testing ? 5174 : 5173,
      strictPort: true,
      proxy,
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      clearMocks: true,
      restoreMocks: true,
    },
  }
})
