import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  // 独立缓存目录：避免符号链接导致的路径解析问题
  cacheDir: resolve(__dirname, '../../.vite-cache/auditor'),
  resolve: {
    alias: [
      { find: '@', replacement: resolve(__dirname, 'src') },
      { find: '@yzh-core', replacement: resolve(__dirname, '../../yzh.vue.core/src') },
      { find: '@share', replacement: resolve(__dirname, '../cert-share/src') }
    ]
  },
  server: {
    host: '127.0.0.1',
    port: 9991,
    // 临时公网隧道（scripts/tunnel/）：Vite >= 5.4.12 会校验 Host 头，
    // 白名单外的域名一律 403 (Blocked request)。前导点 = 匹配该域名全部子域。
    allowedHosts: ['.trycloudflare.com'],
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:9992',
        changeOrigin: true,
        // 实时推送 Hub /api/yzh-msg 需要 WebSocket 升级（SignalR；WS 失败时它也会自动降级 SSE）
        ws: true
      }
    }
  }
})
