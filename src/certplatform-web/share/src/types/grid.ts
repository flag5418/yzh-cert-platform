/**
 * EntityConfig 类型定义 - 后端驱动 UI 的核心结构
 * 与后端 YzhControllerBase.Config 对应
 */

/** 控件类型枚举（与后端 DefineColumnType 对齐） */
export type ControlType =
  | 'TextBox'
  | 'TextArea'
  | 'NumberBox'
  | 'DatePicker'
  | 'DateTimePicker'
  | 'ComboBox'
  | 'DropDownList'
  | 'RadioButtonList'
  | 'CheckBox'
  | 'Switch'
  | 'Upload'
  | 'TreeSelect'
  | 'Cascader'

/** 单条列定义（对应后端 DefineColumn） */
export interface DefineColumn {
  fieldName: string
  desName: string
  width: number
  type: ControlType
  xsFlag: boolean
  bcFlag: boolean
  yxk: boolean
  enable: boolean
  sortable: boolean
  fixed: string
  align: string
  dictCode: string
  options: SelectOption[]
  format: string
  groupIndex: number
}

export interface SelectOption {
  label: string
  value: any
  disabled?: boolean
}

export interface ToolbarConfig {
  add?: boolean
  delete?: boolean
  export?: boolean
  import?: boolean
  customButtons?: ToolbarButton[]
}

export interface ToolbarButton {
  key: string
  text: string
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'info'
  icon?: string
}

export interface RowButtonConfig {
  edit?: boolean
  delete?: boolean
  customButtons?: RowButton[]
}

export interface RowButton {
  key: string
  text: string
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'info'
  icon?: string
  visible?: string
}

export interface EntityConfig {
  configName: string
  tableName: string
  primaryKey: string
  title: string
  layoutColumns: number
  columns: DefineColumn[]
  toolbar: ToolbarConfig
  rowButtons: RowButtonConfig
}

export interface PageRequest {
  page: number
  rows: number
  sort?: string
  order?: 'asc' | 'desc'
  conditions?: Condition[]
}

export interface Condition {
  field: string
  value: any
  operator?: 'eq' | 'neq' | 'like' | 'gt' | 'lt' | 'gte' | 'lte' | 'in'
}

export interface PageResult<T = any> {
  rows: T[]
  total: number
  summary?: Record<string, any>
}
