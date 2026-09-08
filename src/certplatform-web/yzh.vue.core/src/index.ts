// YZH Vue Core - 核心组件库
// 业务无关的通用底层能力

// 组件
export { YzhTable } from './components/table'
export type { YzhTableColumn, YzhTableColumnV4, PageParams, Page, YzhTableDataLoader, YzhTableToolbar, DefaultSort, SearchField } from './components/table/types'
export { YzhForm } from './components/form'
export type { YzhFormField, YzhFieldType } from './components/form/YzhForm.vue'
export { YzhSearchBar, YzhToolbar, YzhPagination, YzhPageLayout, YzhDialog } from './components/layout'
export { YzhEmptyState, YzhStatusBadge, YzhCard } from './components/ui'
export { default as YzhCrudPage } from './components/page/YzhCrudPage.vue'

// API
export { YzhApiClient, yzhApi, tokenStore } from './api/client'
export type { ApiResponse, RequestOptions, YzhApiClientOptions } from './api/client'
export * from './api/auth'

// Composables
export { useTable } from './composables/useTable'
export { useAuth } from './composables/useAuth'

// Types
export * from './types'

// Logic（页面逻辑基类 - Config 驱动渲染的核心，从 share 导出）
export { CrudPageLogic, TreeTableLogic } from '@share/logic'

// Utils
export { default as http, type ApiResponse as HttpApiResponse, type LoginData } from './utils/http'
