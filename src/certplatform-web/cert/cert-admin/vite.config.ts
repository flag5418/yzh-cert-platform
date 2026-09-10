import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  // 独立缓存目录：避免符号链接导致的路径解析问题
  cacheDir: resolve(__dirname, '../../.vite-cache/admin'),
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
      '@yzh-core': resolve(__dirname, '../../yzh.vue.core/src'),
      '@share': resolve(__dirname, '../cert-share/src')
    }
  },
  server: {
    host: '127.0.0.1',
    port: 9990,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:9992',
        changeOrigin: true
      }
    }
  }
})
