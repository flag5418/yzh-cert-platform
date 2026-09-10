// YZH Vue Core - 核心组件库
// 与项目完全无关的通用底层能力
// 不依赖 share / admin / auditor

// 组件
export { YzhForm } from './components/form'
export type { YzhFieldType, YzhFormField } from './components/form/YzhForm.vue'
export {
  YzhDialog,
  YzhPageLayout,
  YzhPagination,
  YzhSearchBar,
  YzhToolbar,
  YzhTree,
  YzhTreeTable,
} from './components/layout'
export { default as YzhCrudPage } from './components/page/YzhCrudPage.vue'
export { YzhTable } from './components/table'
export type {
  DefaultSort,
  Page,
  PageParams,
  SearchField,
  YzhTableColumn,
  YzhTableColumnV4,
  YzhTableDataLoader,
  YzhTableToolbar,
} from './components/table/types'
export { YzhCard, YzhEmptyState, YzhStatusBadge } from './components/ui'

// API 客户端
export { YzhApiClient, tokenStore, yzhApi } from './api/client'
export type {
  ApiResponse,
  RequestOptions,
  YzhApiClientOptions,
} from './api/client'

// Composables
export { useAuth } from './composables/useAuth'
export { useTable } from './composables/useTable'

// 页面逻辑基类
export * from './logic'

// 通用类型
export * from './types'

// 通用工具
export * from './utils/treeOps'
export * from './utils/treeUtils'
