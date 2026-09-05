/**
 * YZH 布局组件统一导出
 */
import YzhPageLayout from './YzhPageLayout.vue'
import YzhSearchBar from './YzhSearchBar.vue'
import YzhToolbar from './YzhToolbar.vue'
import YzhPagination from './YzhPagination.vue'
import YzhMenu from './YzhMenu.vue'

export {
  YzhPageLayout,
  YzhSearchBar,
  YzhToolbar,
  YzhPagination,
  YzhMenu
}

// 批量注册
export default {
  install(app: any) {
    app.component('YzhPageLayout', YzhPageLayout)
    app.component('YzhSearchBar', YzhSearchBar)
    app.component('YzhToolbar', YzhToolbar)
    app.component('YzhPagination', YzhPagination)
    app.component('YzhMenu', YzhMenu)
  }
}
