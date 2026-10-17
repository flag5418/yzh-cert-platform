import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: [
      { find: '@', replacement: resolve(__dirname, 'src') },
      { find: '@yzh-core', replacement: resolve(__dirname, '../../yzh.vue.core/src') },
      { find: '@share', replacement: resolve(__dirname, '../cert-share/src') }
    ]
  },
  test: {
    environment: 'happy-dom',
    globals: true,
    include: ['src/**/*.test.ts', 'src/**/*.spec.ts'],
  }
})
