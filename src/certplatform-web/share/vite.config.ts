import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@share': resolve(__dirname, 'src'),
      '@yzh-core': resolve(__dirname, '../yzh.vue.core/src')
    }
  }
})
