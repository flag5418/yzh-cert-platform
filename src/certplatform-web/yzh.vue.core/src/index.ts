// YZH Vue Core - 核心组件库
// 与项目完全无关的通用底层能力
// 不依赖 share / admin / auditor

// 组件
export { YzhForm, YzhFormDialog } from './components/form'
export type { YzhFieldType, YzhFormField } from './components/form/YzhForm.vue'
export {
  YzhDialog,
  YzhPageLayout,
  YzhPagination,
  YzhSearchBar,
  YzhToolbar,
  YzhTree,
  YzhTreeTableLayout,
  YzhTreeTableSelector,
  YzhTreeTableCheckSelector,
} from './components/layout'
export type { YzhTreeNode } from './components/layout/YzhTree.vue'
export { YzhTable, YzhTreeTable } from './components/table'
export type {
  DefaultSort,
  Page,
  PageParams,
  SearchField,
  YzhAction,
  YzhActionType,
  YzhNodeActions,
  YzhRowActionResolver,
  YzhRowActions,
  YzhTableColumn,
  YzhTableColumnV4,
  YzhTableDataLoader,
  YzhTableToolbar,
  YzhToolbarActions,
} from './components/table/types'
export { YzhCard, YzhEmptyState, YzhStatusBadge } from './components/ui'

// 适配层（AD：EntityConfig → 组件契约，纯函数）
export * from './adapters/entityAdapters'

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
export * from './composables/index'

// 页面逻辑内核（新命名 *Core + 废弃别名）
export * from './logic'

// 通用类型
// 注：只从 contracts 导出 ApiResponse（与新架构契约一致）；
// 旧版 types/ApiResponse（status/msg 形状）已弃用，避免 TS2308 双源歧义
export * from './types/Page'
export * from './types/contracts'
export * from './types/tree'
export * from './types/association'

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
export {
  toCamelCase,
  toPascalCase,
  pascalCaseFormData,
  rowToFormData,
} from './utils/case'
// 信封判定原语（铁律 F-1/F-2）+ 取消确认（铁律 F-4）—— 页面 handler 直接可用
export {
  BizError,
  envelopeErrorText,
  expectOk,
  isBizError,
  unwrap,
  unwrapOk,
} from './utils/apiResponse'
export { confirmOrFalse } from './utils/confirm'
