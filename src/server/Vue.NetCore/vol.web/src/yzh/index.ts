// ============================================================
//  YZH Framework —— 统一出口
//  V2.0: 从 Vol 框架解耦，基于 Element Plus 原生组件
//  V3.0: 数据库配置驱动，原子组件组合模式
// ============================================================

// ====== V3.0 核心组件（数据库驱动） ======
export { default as YzhCrudV3 } from './components/YzhCrudV3.vue'
export { default as YzhToolbar } from './components/YzhToolbar.vue'
export type { YzhButtonConfig, YzhColumnItem as YzhToolbarColumnItem } from './components/YzhToolbar.vue'
export { default as YzhDataTable } from './components/YzhDataTable.vue'
export type { YzhTableColumn } from './components/YzhDataTable.vue'
export { default as YzhEditDialog } from './components/YzhEditDialog.vue'
export { default as YzhSearchBar } from './components/YzhSearchBar.vue'
export { default as YzhPagination } from './components/YzhPagination.vue'

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
export { YZHRowDiff, replaceByKey, removeByKeys, insertByOrder } from './core/YZHRowDiff'
export { createDefaultLifecycles, runGuard } from './core/YZHPageLifecycle'

// ====== V3.0 配置加载器 ======
export { loadPageConfig, clearPageConfigCache, clearAllConfigCache, getCachedConfig } from './core/YZHConfigLoader'

// ====== Composables ======
export { useYZHEditMode } from './composables/useYZHEditMode'
export { useYZHIncrementSync } from './composables/useYZHIncrementSync'

// ====== 预设配置 ======
export { mergeDefaultButtons, DEFAULT_BUTTONS } from './presets/defaultButtons'

// ====== 统一图标管理（V3 新增） ======
export { YzhIcon } from './icons'
export {
  IconBack, IconForward, IconMenu, IconClose,
  IconAdd, IconDelete, IconEdit, IconEditPen, IconSearch, IconRefresh,
  IconDownload, IconUpload, IconCopy,
  IconFolder, IconFolderOpen, IconFolderChecked, IconFile, IconFileChecked,
  IconSuccess, IconCircleSuccess, IconError, IconWarning, IconInfo,
  IconLoading, IconPending, IconHelp, IconSetting, IconAnalyze, IconPrompt,
} from './icons'

// ====== 基础组件库（V3 新增，对齐 vidlang components/ui） ======
export { YzhBaseCard, YzhTitledCard, YzhEmptyState, YzhStatusBadge } from './components/ui'
