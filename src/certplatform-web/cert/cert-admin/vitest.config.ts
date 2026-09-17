import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
      '@share': resolve(__dirname, '../cert-share/src'),
      '@yzh-core': resolve(__dirname, '../../yzh.vue.core/src'),
    }
  },
  test: {
    environment: 'happy-dom',
    globals: true,
    include: ['src/**/*.test.ts', 'src/**/*.spec.ts'],
  }
})
