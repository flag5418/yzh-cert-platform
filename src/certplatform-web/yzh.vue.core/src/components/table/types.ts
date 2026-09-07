/**
 * YzhTable - 自研表格组件（替代 vol 的 view-grid）
 *
 * 核心理念：业务侧只描述"显示什么字段、字段如何格式化"，不关心分页/排序/选择等通用能力。
 * MVP 版只覆盖 80% 场景，剩余 20% 通过插槽扩展。
 */

import type { PropType } from 'vue'

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
