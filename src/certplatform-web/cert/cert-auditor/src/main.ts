import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import App from './App.vue'
import router from './router'

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
