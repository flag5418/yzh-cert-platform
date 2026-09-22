/**
 * YzhTable - 自研表格组件（替代 vol 的 view-grid）
 *
 * 核心理念：业务侧只描述"显示什么字段、字段如何格式化"，不关心分页/排序/选择等通用能力。
 * MVP 版只覆盖 80% 场景，剩余 20% 通过插槽扩展。
 */

import type { PropType } from 'vue'

// ========================================================
// YzhAction 描述符（C-A2）
// 统一按钮/动作声明：工具栏、行按钮、树节点动作共用同一形状。
// 颜色语义外置：type 由调用方声明，组件不内置 edit/delete→颜色 的业务假设（C-A4）。
// ========================================================

/** 按钮语义类型（element-plus button type） */
export type YzhActionType = 'primary' | 'success' | 'warning' | 'danger' | 'info'

/** 通用动作描述符 */
export interface YzhAction {
  /** 动作唯一键（如 add/edit/delete/toggle-valid 或自定义方法名） */
  key: string
  /** 显示文字 */
  text: string
  /** 按钮类型（颜色语义由调用方声明） */
  type?: YzhActionType
  /** 图标（element-plus 图标组件名） */
  icon?: string
  /** 是否禁用 */
  disabled?: boolean
  /** 是否显示（默认 true） */
  visible?: boolean
  /** 工具栏分组（默认 left） */
  group?: 'left' | 'right'
  /** 点击时是否弹确认框（文案由调用方通过 confirmText 提供） */
  confirm?: string
  /** 是否作为 danger 样式下拉项（树节点动作用） */
  danger?: boolean
  /** 透传给处理器的附加载荷 */
  payload?: any
}

/** 行动作描述符（在 YzhAction 基础上支持按行求值） */
export type YzhRowActionResolver<T = any> = (row: T) => YzhAction[]

/** 行动作输入：静态数组或按行解析函数 */
export type YzhRowActions<T = any> = YzhAction[] | YzhRowActionResolver<T>

/** 工具栏动作输入 */
export type YzhToolbarActions = YzhAction[]

/** 树节点动作输入：静态数组或按节点解析函数 */
export type YzhNodeActions = YzhAction[] | ((node: any) => YzhAction[])

// ========================================================
// 表格列定义
// ========================================================

/** 表格列定义 */
export interface YzhTableColumn<T = any> {
  /** 字段名（数据源中的 key） */
  prop: keyof T | string
  /** 列标题 */
  label: string
  /** 宽度 */
  width?: number | string
  /** 最小宽度 */
  minWidth?: number | string
  /** 固定列 */
  fixed?: 'left' | 'right'
  /** 是否可排序 */
  sortable?: boolean | 'custom'
  /** 对齐方式 */
  align?: 'left' | 'center' | 'right'
  /** 自定义格式化 */
  formatter?: (value: any, row: T, index: number) => any
  /** 是否使用自定义插槽（用于操作列等，插槽名为 column-{prop}） */
  slot?: boolean
  /** 是否隐藏 */
  hidden?: boolean
  /** 自定义 class */
  className?: string
  /** 是否省略号 */
  showOverflowTooltip?: boolean
  /** 字典编码（自动从全局字典池翻译） */
  dictCode?: string
  /** 标签类型（仅当 dictCode 生效时）：success/warning/info/primary/danger */
  tagType?: 'success' | 'warning' | 'info' | 'primary' | 'danger'
  /** 是否掩码显示（敏感字段如 key/secret/password） */
  mask?: boolean
  /** 通用标签渲染（C-A6）：值 → 显示文字 / 标签类型的映射，由调用方传入，组件不内置业务语义 */
  tagMap?: Record<string | number, string>
  /** render:'tag' 时的标签类型映射（可选） */
  tagTypeMap?: Record<string | number, YzhActionType>
}

// V4 命名空间别名（避免和旧版 YzhDataTable 冲突）
export type YzhTableColumnV4<T = any> = YzhTableColumn<T>

/** 分页请求参数 */
export interface PageParams {
  page: number
  rows: number
  sort?: string
  order?: 'asc' | 'desc' | 'ascending' | 'descending'
  [key: string]: any
}

/** 分页响应 */
export interface Page<T = any> {
  rows: T[]
  total: number
}

/** 数据加载器签名 */
export type YzhTableDataLoader<T = any> = (params: PageParams) => Promise<Page<T>>

/** 工具栏配置 */
export interface YzhTableToolbar {
  refresh?: boolean
  columnSetting?: boolean
  density?: boolean
}

export type YzhTableToolbarV4 = YzhTableToolbar

/** 默认排序 */
export interface DefaultSort {
  prop: string
  order: 'asc' | 'desc' | 'ascending' | 'descending'
}

/** 搜索字段定义 */
export interface SearchField {
  prop: string
  label: string
  type?: 'text' | 'number' | 'select' | 'date' | 'dateRange'
  placeholder?: string
  options?: Array<{ label: string; value: any }>
  defaultValue?: any
  width?: string
}

export const TableProps = {
  columns: {
    type: Array as PropType<YzhTableColumn[]>,
    default: () => []
  },
  dataLoader: {
    type: Function as PropType<YzhTableDataLoader>,
    required: true
  }
} as const
