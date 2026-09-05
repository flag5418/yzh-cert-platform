// ============================================================
//  YZH Framework —— 统一出口
//  V2.0: 从 Vol 框架解耦，基于 Element Plus 原生组件
//  V3.0: 数据库配置驱动，原子组件组合模式
// ============================================================

// ====== V3.0 核心组件（数据库驱动） ======
export { default as YzhCrudV3 } from './components/YzhCrudV3.vue'
export { default as YzhDataTable } from './components/YzhDataTable.vue'
export type { YzhTableColumn } from './components/YzhDataTable.vue'
export { default as YzhEditDialog } from './components/YzhEditDialog.vue'
export { default as YzhPagination } from './components/YzhPagination.vue'
export { default as YzhSearchBar } from './components/YzhSearchBar.vue'
export { default as YzhToolbar } from './components/YzhToolbar.vue'
export type {
  YzhButtonConfig,
  YzhColumnItem as YzhToolbarColumnItem
} from './components/YzhToolbar.vue'

// ====== V2.0 兼容组件 ======
export { default as YzhCrudTable } from './components/YzhCrudTable.vue'
export { default as YzhTreeTable } from './components/YzhTreeTable.vue'

// ====== 原子表单组件 ======
export { default as YzhFormField } from './components/YzhFormField.vue'
export { default as YzhFormGrid } from './components/YzhFormGrid.vue'

// ====== 类型定义 ======
export * from './types'

// ====== 核心 TS 类（可独立使用） ======
export { YZHBaseApiClient } from './core/YZHBaseApiClient'
export { YZHEditGuard } from './core/YZHEditGuard'
export { createDefaultLifecycles, runGuard } from './core/YZHPageLifecycle'
export { YZHRowDiff, insertByOrder, removeByKeys, replaceByKey } from './core/YZHRowDiff'

// ====== V3.0 配置加载器 ======
export {
  clearAllConfigCache,
  clearPageConfigCache,
  getCachedConfig,
  loadPageConfig
} from './core/YZHConfigLoader'

// ====== Composables ======
export { useYZHEditMode } from './composables/useYZHEditMode'
export { useYZHIncrementSync } from './composables/useYZHIncrementSync'

// ====== 队列中心（通用后台任务队列） ======
export { useYzhQueue } from './composables/useYzhQueue'

// ====== 队列监控页面（通用） ======
export { default as YzhQueueMonitor } from './views/QueueMonitor/index.vue'

// ====== 预设配置 ======
export { DEFAULT_BUTTONS, mergeDefaultButtons } from './presets/defaultButtons'

// ====== 统一图标管理（V3 新增） ======
export {
  IconAdd,
  IconAnalyze,
  IconBack,
  IconCalendar,
  IconCircleSuccess,
  IconClose,
  IconCode,
  IconCopy,
  IconDelete,
  IconDocument,
  IconDownload,
  IconEdit,
  IconEditPen,
  IconError,
  IconFile,
  IconFileChecked,
  IconFolder,
  IconFolderAdd,
  IconFolderChecked,
  IconFolderOpen,
  IconForward,
  IconGrid,
  IconHelp,
  IconInfo,
  IconLink,
  IconList,
  IconLoading,
  IconMenu,
  IconOfficeBuilding,
  IconPending,
  IconPrompt,
  IconRefresh,
  IconSearch,
  IconSetting,
  IconSuccess,
  IconUpload,
  IconWarning,
  YzhIcon
} from './icons'

// ====== 基础组件库（V3 新增，对齐 vidlang components/ui） ======
export {
  YzhBaseCard,
  YzhEmptyState,
  YzhLockStatus,
  YzhStatusBadge,
  YzhTitledCard
} from './components/ui'

// ====== 机构-标准-阶段 左侧公共树组件 ======
export { default as YzhStdTree } from './components/YzhStdTree.vue'

// ====== V4.0 全新架构（彻底替代 vol view-grid） ======
export { YzhApiClient, tokenStore, yzhApi } from './api'
export type { ApiResponse, RequestOptions, YzhApiClientOptions } from './api'
export type { CertStage, CertStageSearchParams } from './api/cert-stage'
export type { CertBodySearchParams, CertificationBody } from './api/certification-body'
export type { Enterprise, EnterpriseSearchParams } from './api/enterprise'
export type { ISOClause, ISOClauseSearchParams } from './api/iso-clause'
export type { ISOStandard, ISOStandardSearchParams } from './api/iso-standard'
export type { DeptSearchParams, SysDepartment } from './api/system-dept'
export type { DictSearchParams, SysDictionary, SysDictionaryList } from './api/system-dict'
export type { LogSearchParams, SysLog } from './api/system-log'
export type { SysMenu } from './api/system-menu'
export type { RoleSearchParams, SysRole } from './api/system-role'
export type { SysUser, UserSearchParams } from './api/system-user'
export { YzhForm } from './components/form'
export type { YzhFieldType, YzhFormField as YzhFormFieldV4 } from './components/form'
export { default as YzhPaginationV4 } from './components/layout/YzhPagination.vue'
export { default as YzhSearchBarV4 } from './components/layout/YzhSearchBar.vue'
export { default as YzhToolbarV4 } from './components/layout/YzhToolbar.vue'
export { default as YzhPageLayout } from './components/layout/YzhPageLayout.vue'
export type {
  DefaultSort,
  Page,
  PageParams,
  SearchField,
  YzhTableColumn as YzhTableColumnV4,
  YzhTableDataLoader as YzhTableDataLoaderV4,
  YzhTableToolbar as YzhTableToolbarV4
} from './components/table/types'
export { default as YzhTable } from './components/table/YzhTable.vue'
export { dictStore } from './store/dict'

// ====== 文件上传组件（支持文件夹拖拽） ======
export { default as YzhFolderUpload } from './components/YzhFolderUpload/index.vue'
