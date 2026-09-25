import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      // ⚠️ core 只允许 `@yzh-core` 自指 —— ⛔ 不得加 `@share` / `@` 等**宿主向** alias
      //    （守卫 R10：core 新层禁宿主反向依赖）。
      //    原有 `'@share': resolve(__dirname, '../share/src')` 已于 2026-09-24 删除：
      //    ① 路径本身是错的（`../share` 不存在，实际目录是 `../cert-share`）；
      //    ② 即便路径正确也违反 R10 —— core 全源码对 `@share` 的引用**只存在于注释**里。
      '@yzh-core': resolve(__dirname, 'src')
    }
  },
  build: {
    lib: {
      entry: resolve(__dirname, 'src/index.ts'),
      name: 'YzhVueCore',
      formats: ['es'],
      fileName: 'yzh-vue-core'
    },
    rollupOptions: {
      external: ['vue', 'vue-router', 'element-plus'],
      output: {
        globals: {
          vue: 'Vue',
          'vue-router': 'VueRouter',
          'element-plus': 'ElementPlus'
        }
      }
    }
  }
})
