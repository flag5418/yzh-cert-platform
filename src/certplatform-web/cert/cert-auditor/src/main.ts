import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import { installApiErrorBoundary } from '@yzh-core/api/errorBoundary'
import { initTheme } from '@yzh-core/composables/useTheme'
import { ElMessage } from 'element-plus'
// ★ 样式穿透三层（顺序不可颠倒）：element-plus 基线 → core 令牌默认层 → 宿主覆盖层
import '@yzh-core/assets/css/tokens.css'
import './assets/css/main.css'
import App from './App.vue'
import router from './router'

// 宿主注入后台地址 + 错误边界（契约：core 零硬编码地址 / D6：onError 只做 401 副作用）
// unhandledrejection 兜底在同一入口内安装，防「点了没反应」的静默失败
installApiErrorBoundary({
  baseURL: (import.meta as any).env?.VITE_API_BASE ?? '',
  onUnauthorized: () => router.push('/login').catch(() => {}),
})

const app = createApp(App)

// ★ Vue 全局异常处理：捕获渲染/生命周期/watcher 中未被捕获的同步错误
//   与 installApiErrorBoundary 的 unhandledrejection 互补：
//   - unhandledrejection 兜底 Promise 拒绝
//   - errorHandler 兜底 Vue 组件错误
//   两者协作避免「页面白屏/无响应但无任何提示」
app.config.errorHandler = (err: unknown, _instance: any, info: string) => {
  const e = err as Error & { handled?: boolean }
  // 已被 unhandledrejection / catch 方标记处理过的 → 跳过，防双弹
  if (e?.handled === true) return
  // 控制台保留完整堆栈，便于排查
  console.error(`[VueError] ${info}`, err, _instance?.$options?.__name || _instance?.$options?.name)
  // 用户友好提示（仅非生产环境或关键错误显示详情）
  const detail = import.meta.env.DEV && e?.message ? `: ${e.message}` : ''
  ElMessage.error(`页面渲染异常${detail}`)
}

// 回放缓存主题（localStorage `yzh-theme`），须在挂载前执行，避免首屏亮/暗闪烁
initTheme()

// ★ 必须全局注册 Element Plus 图标（勿删）
//   侧边栏菜单图标由 `<component :is="formatMenuIcon(menu.icon)" />` **动态**渲染，
//   未注册时 Vue 会刷 `Failed to resolve component: Xxx` 警告 —— 不报错，
//   但会污染控制台，导致调试时把真实问题淹没在噪音里。
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(createPinia())
app.use(router)
// 中文语言包：否则 ElMessageBox / 表格空态 / 分页等内置文案为英文
app.use(ElementPlus, { locale: zhCn })
app.mount('#app')
