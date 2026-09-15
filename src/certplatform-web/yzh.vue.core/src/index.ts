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
  YzhTreeTableSelector,
  YzhTreeTableCheckSelector,
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

// 文件存储（通用上传/下载能力）
export {
  uploadFile,
  uploadFileBatch,
  getFileUrl,
  deleteFile as deleteStorageFile,
  fileExists,
  listFiles,
} from './api/file-storage'

// Composables
export { useAuth } from './composables/useAuth'
export { useTable } from './composables/useTable'

// 页面逻辑基类
export * from './logic'

// 通用类型
// 注：只从 contracts 导出 ApiResponse（与新架构契约一致）；
// 旧版 types/ApiResponse（status/msg 形状）已弃用，避免 TS2308 双源歧义
export * from './types/Page'
export * from './types/contracts'
export * from './types/tree'

// 通用工具
// 注：treeOps 与 treeUtils 均导出同名 validate，root 出口改名消歧（TS2308）
// treeOps 独有能力（addNode/removeSubtree/moveSubtree/mergeRoots/diff/flatten）
export {
  addNode,
  removeSubtree,
  moveSubtree,
  updateNode,
  mergeRoots,
  diff,
  flatten,
  validate as validateTreeOps,
} from './utils/treeOps'
export * from './utils/treeUtils'
