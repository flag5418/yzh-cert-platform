import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import { configureYzhApi } from '@yzh-core/api/client'
// ★ 样式穿透三层（顺序不可颠倒）：element-plus 基线 → core 令牌默认层 → 宿主覆盖层
import '@yzh-core/assets/css/tokens.css'
import './assets/css/main.css'
import App from './App.vue'
import router from './router'

// 宿主注入后台地址（契约：core 零硬编码地址）——缺省 '' 走 vite proxy / 同源
configureYzhApi({ baseURL: (import.meta as any).env?.VITE_API_BASE ?? '' })

const app = createApp(App)

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
