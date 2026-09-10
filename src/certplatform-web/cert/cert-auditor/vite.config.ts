import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
      '@yzh-core': resolve(__dirname, '../../yzh.vue.core/src'),
      '@share': resolve(__dirname, '../cert-share/src')
    }
  },
  server: {
    port: 9991,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:9992',
        changeOrigin: true
      }
    }
  }
})
