/**
 * YZH 布局组件类型定义
 */

/**
 * 菜单节点
 */
export interface MenuNode {
  /** 菜单 ID */
  id: string | number
  /** 菜单名称 */
  label: string
  /** 菜单路径 */
  path?: string
  /** 菜单图标 */
  icon?: string
  /** 子菜单 */
  children?: MenuNode[]
  /** 是否启用 */
  enable?: number
  /** 查询参数 */
  query?: Record<string, any>
}

/**
 * 搜索字段
 */
export interface SearchField {
  /** 字段名 */
  prop: string
  /** 字段标签 */
  label: string
  /** 字段类型 */
  type?: 'text' | 'number' | 'select' | 'date' | 'dateRange'
  /** 占位符 */
  placeholder?: string
  /** 默认值 */
  defaultValue?: any
  /** 选项（select 类型） */
  options?: { label: string; value: any }[]
}

/**
 * 表格列定义
 */
export interface YzhTableColumn<T = any> {
  /** 字段名 */
  prop: string
  /** 列标题 */
  label: string
  /** 列宽度 */
  width?: number | string
  /** 最小宽度 */
  minWidth?: number | string
  /** 是否固定 */
  fixed?: boolean | 'left' | 'right'
  /** 是否可排序 */
  sortable?: boolean
  /** 对齐方式 */
  align?: 'left' | 'center' | 'right'
  /** 是否隐藏 */
  hidden?: boolean
  /** 字典编码 */
  dictCode?: string
  /** 标签类型 */
  tagType?: 'success' | 'warning' | 'danger' | 'info'
  /** 格式化函数 */
  formatter?: (value: any, row: T, index: number) => string
  /** 是否使用插槽 */
  slot?: boolean
  /** 自定义类名 */
  className?: string
}

/**
 * 分页参数
 */
export interface PageParams {
  page: number
  rows: number
  sort?: string
  order?: 'asc' | 'desc'
  [key: string]: any
}

/**
 * 分页数据
 */
export interface Page<T> {
  rows: T[]
  total: number
  page?: number
  pageSize?: number
}

/**
 * 表格工具栏配置
 */
export interface YzhTableToolbar {
  refresh?: boolean
  columnSetting?: boolean
  density?: boolean
}

/**
 * 默认排序
 */
export interface DefaultSort {
  prop: string
  order: 'asc' | 'desc'
}

/**
 * 表格数据加载器
 */
export type YzhTableDataLoader<T> = (params: PageParams) => Promise<Page<T>>
