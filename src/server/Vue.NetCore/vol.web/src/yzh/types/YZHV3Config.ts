// ============================================================
//  YZH Framework V3.0 —— 数据库配置类型定义
//  对应后端表: yzh_page_config + yzh_field_config
// ============================================================

// ====== 控件类型枚举 ======
export type YzhControlType =
  | 'input'
  | 'textarea'
  | 'select'
  | 'number'
  | 'decimal'
  | 'date'
  | 'switch'
  | 'cascader'
  | 'treeSelect'
  | 'file'
  | 'img'
  | 'slot'
  | 'hidden'

// ====== 搜索模式 ======
export type YzhSearchMode = 'fixed' | 'togglable' | 'hidden'

// ====== 对齐方式 ======
export type YzhAlign = 'left' | 'center' | 'right'

// ====== 页面级配置（来自 yzh_page_config） ======
export interface IYzhPageMeta {
  // 基础标识
  pageKey: string
  pageTitle: string
  entityName: string
  tableName: string
  controllerName: string

  // 主键
  keyField: string
  keyFieldType: 'number' | 'guid' | 'string'

  // 排序
  sortField: string
  sortOrder: 'asc' | 'desc'

  // 弹窗配置
  dialogWidth: number
  dialogMaxHeight: string
  dialogLabelWidth: number

  // 表格配置
  rowHeight: 'default' | 'large' | 'small'
  stripe: boolean
  showRowNumber: boolean

  // 搜索区
  searchMode: YzhSearchMode

  // 工具栏按钮 (JSON 解析后的数组)
  visibleButtons: string[]

  // 功能开关
  showActionColumn: boolean
  checkboxSelection: boolean
  incrementalUpdate: boolean
}

// ====== 字段级配置（来自 yzh_field_config） ======
export interface IYzhFieldConfig {
  // 标识
  fieldName: string           // 数据库字段名 (与实体属性一致)
  fieldAlias: string          // 组件命名标识 (默认同 fieldName)

  // === A. 表格列配置 ===
  xsFlag: boolean             // 表格显示标志
  columnSxh: number           // 列显示序号 (越小越靠左)
  columnTitle: string         // 列头标题
  columnWidth: number         // 列宽 (px)
  columnFixed?: 'left' | 'right' | null  // 列固定位置
  sortable: boolean           // 可排序
  columnFormatter?: string    // 自定义列格式化器名称
  showOverflow: boolean       // 文本溢出省略号
  align: YzhAlign             // 对齐方式

  // === B. 弹窗表单 / Grid 布局 ===
  bcFlag: boolean             // 保存标志 (true=保存到DB, false=视图字段不保存)
  formTitle: string           // 表单标签
  controlType: YzhControlType // 控件类型
  gridRow: number             // Grid 行号 (从0开始)
  gridCol: number             // Grid 列号 (从0开始)
  gridRowSpan: number         // 跨行数
  gridColSpan: number         // 跨列数
  required: boolean           // 必填
  maxLength: number           // 最大长度 (0=不限)
  placeholder: string         // 占位文本
  defaultValue: string        // 默认值
  readonly: boolean           // 只读
  disabled: boolean           // 禁用
  precision?: number          // 小数精度 (number/decimal)
  minVal?: number             // 最小值
  maxVal?: number             // 最大值
  textareaRows: number        // 文本域行数

  // === 数据源 ===
  dataKey?: string            // 字典编号 (select/treeSelect/cascader)
  remoteUrl?: string          // 远程数据源 URL

  // === 业务控制 ===
  groupIndex: number          // 工作流阶段分组 (0=全阶段 9=系统字段)

  // === C. 搜索区配置 ===
  searchFlag: boolean         // 作为搜索条件
  searchTitle: string         // 搜索标签
  searchPlaceholder: string   // 搜索占位文本
  searchControlType?: YzhControlType  // 搜索控件类型 (默认取 controlType)
  searchWidth: number         // 搜索控件宽度 (px)
}

// ====== 完整页面 UI 配置（API 返回结构）=====
export interface IYzhPageUIConfig {
  pageMeta: IYzhPageMeta
  fieldConfigs: IYzhFieldConfig[]
}

// ====== 派生便捷类型 ======

/** 从 fieldConfigs 中筛选出的表格列配置 */
export interface IYzhColumnConfig extends Pick<IYzhFieldConfig,
  'fieldAlias' | 'columnTitle' | 'columnWidth' | 'columnFixed' |
  'sortable' | 'showOverflow' | 'align' | 'columnFormatter'
> {
  /** 内部使用：原始字段名 */
  _fieldName: string
  /** 是否在表格中显示 (xsFlag) */
  visible: boolean
  /** 显示顺序 */
  order: number
}

/** 从 fieldConfigs 中筛选出的表单字段配置 */
export interface IYzhFormFieldConfig extends Pick<IYzhFieldConfig,
  'fieldName' | 'fieldAlias' | 'formTitle' | 'controlType' |
  'gridRow' | 'gridCol' | 'gridRowSpan' | 'gridColSpan' |
  'required' | 'maxLength' | 'placeholder' | 'defaultValue' |
  'readonly' | 'disabled' | 'precision' | 'minVal' | 'maxVal' |
  'textareaRows' | 'dataKey' | 'remoteUrl' | 'groupIndex' | 'bcFlag'
> {}

/** 从 fieldConfigs 中筛选出的搜索条件配置 */
export interface IYzhSearchFieldConfig extends Pick<IYzhFieldConfig,
  'fieldName' | 'fieldAlias' | 'searchTitle' | 'searchPlaceholder' |
  'searchControlType' | 'searchWidth' | 'dataKey'
> {
  /** 实际使用的控件类型 */
  controlType: YzhControlType
}

// ====== 精确控制接口（§4.8）=====

/** 按钮实例接口 */
export interface IButtonInstance {
  key: string
  visible: Ref<boolean>
  disabled: Ref<boolean>
  loading: Ref<boolean>
  onClick?: () => void
}

/** 列实例接口 */
export interface IColumnInstance {
  fieldAlias: string
  visible: Ref<boolean>
  width: Ref<number>
  formatter?: Function
}

/** 表单字段实例接口 */
export interface IFieldInstance {
  fieldAlias: string
  value: Ref<any>
  disabled: Ref<boolean>
  readonly: Ref<boolean>
  visible: Ref<boolean>
  validate: () => Promise<boolean>
  focus: () => void
  reset: () => void
}
