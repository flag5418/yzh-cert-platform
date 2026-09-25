import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import { installApiErrorBoundary } from '@yzh-core/api/errorBoundary'
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

// 注册所有 Element Plus 图标
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(createPinia())
app.use(router)
// 中文语言包：否则 ElMessageBox / 表格空态 / 分页 / 日期选择器等内置文案为英文
app.use(ElementPlus, { locale: zhCn })
app.mount('#app')
