/**
 * YZH 图标管理导出
 */
import YzhIcon from '@/components/icons/YzhIcon.vue'
import YzhIcons, { getElementIcon, getBootstrapIconClass } from '@/assets/icons/index.js'

export {
  YzhIcon,
  YzhIcons,
  getElementIcon,
  getBootstrapIconClass
}

// 全局注册图标组件
export default {
  install(app) {
    app.component('YzhIcon', YzhIcon)
  }
}
