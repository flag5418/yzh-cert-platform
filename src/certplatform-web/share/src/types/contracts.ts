/**
 * 前后端统一契约类型定义（V1.1）
 * 对应后端 YZH.Core.Stand/Models/Contracts.cs 和 YzhControllerBase API
 *
 * 包括：
 * - 过滤查询（/filter）
 * - 导出导入（/export / /import）
 * - 树结构（/tree/*）
 * - 配置驱动 UI（/config）
 */

// ========================================================
// 一、通用响应包装
// ========================================================

/** API 统一响应 */
export interface ApiResponse<T = any> {
  success: boolean
  message: string
  data: T
  code: number
  timestamp: number
}

/** 分页查询结果 */
export interface PagedResult<T = any> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

// ========================================================
// 二、过滤查询（/filter）
// ========================================================

/** 过滤条件项 */
export interface FilterItem {
  /** 字段名（对应实体属性） */
  field: string
  /** 操作符：eq/neq/gt/gte/lt/lte/like/in/isnull/isnotnull */
  operator: 'eq' | 'neq' | 'gt' | 'gte' | 'lt' | 'lte' | 'like' | 'in' | 'isnull' | 'isnotnull'
  /** 值（in 操作符可为数组或逗号分隔字符串） */
  value?: any
}

/** 过滤查询请求 */
export interface FilterRequest {
  page: number
  pageSize: number
  sortField?: string
  sortOrder?: 'asc' | 'desc'
  filters: FilterItem[]
}

// ========================================================
// 三、导出导入
// ========================================================

/** 导出请求 */
export interface ExportRequest {
  /** 过滤条件（同 FilterRequest.filters） */
  filters: FilterItem[]
  /** 导出格式：excel/csv */
  format: 'excel' | 'csv'
  /** 导出字段（为空导出全部） */
  fields?: string[]
}

/** 导入结果 */
export interface ImportResult {
  totalRows: number
  successRows: number
  failedRows: number
  errors: ImportError[]
}

/** 导入错误 */
export interface ImportError {
  rowIndex: number
  field: string
  message: string
}

/** 导入模板下载请求 */
export interface ImportTemplateRequest {
  /** 字段列表（可选） */
  fields?: string[]
}

// ========================================================
// 四、树结构 API
// ========================================================

/** 树节点 DTO（后端标准响应） */
export interface TreeItemDto {
  code: string
  name: string
  parentCode: string | null
  nodeType?: string
  isLeaf: boolean
  level: number
  sort?: number
  /** 扩展业务字段（enable、remark 等） */
  extra?: Record<string, any>
  /** 子节点（全量加载时内嵌） */
  children?: TreeItemDto[]
}

/** 树懒加载请求 */
export interface TreeChildrenRequest {
  parentCode: string
  level: number
}

/** 树节点操作请求 */
export interface TreeNodeActionRequest {
  /** 目标节点编码 */
  code: string
  /** 父节点编码（新增/移动时使用） */
  parentCode?: string
  /** 附加参数 */
  extra?: Record<string, any>
}

/** 树行为配置（后端返回，对应 YZH.Core.Stand/Models/TreeConfig） */
export interface TreeBehaviorConfig {
  /** 是否懒加载 */
  lazy: boolean
  /** 是否允许编辑树节点 */
  allowEdit: boolean
  /** 是否允许删除树节点 */
  allowDelete: boolean
  /** 是否允许修改树节点名称 */
  allowRename: boolean
  /** 根节点父编码值 */
  rootParentCode: string | null
  /** 树节点名称字段 */
  nameField: string
  /** 树节点编码字段 */
  codeField: string
  /** 树节点父编码字段 */
  parentCodeField: string
  /** 关联字段（表格中用于过滤的字段名） */
  relateField: string
  /** 未选中树节点时表格行为：empty=不显示 / all=显示全部 */
  noSelectionBehavior: 'empty' | 'all'
  /** 最大层级深度（0=不限） */
  maxLevel: number
  /** 是否允许删除含子节点的父节点 */
  allowDeleteWithChildren: boolean
  /** 是否只能选择叶子节点 */
  onlyLeafSelectable?: boolean
}

// ========================================================
// 五、配置驱动 UI（/config）
// ========================================================

/** 搜索条件配置 */
export interface SearchFieldConfig {
  label: string
  field: string
  operator: 'like' | 'eq' | 'gt' | 'lt' | 'in'
  controlType: 'input' | 'select' | 'date' | 'cascader'
  options?: Array<{ label: string; value: any }>
  width: number
}

/** 实体配置（后端返回，驱动前端 UI） */
export interface EntityConfigDto {
  /** 页面标题 */
  title: string
  /** 填充模式：autoFix=等比例填满，pixFix=按像素宽度 */
  fillMode?: 'autoFix' | 'pixFix'
  /** 列定义（同时驱动表格和表单） */
  columns: ColumnConfig[]
  /**
   * 空实体模板（camelCase 字段名 → 默认值）
   * 来源：后端反射实体自动生成
   * 用途：前端直接用这个对象初始化 formData，不再手写 interface
   *
   * 示例：
   * {
   *   "code": "",
   *   "userName": "",
   *   "roleId": 0,
   *   "enable": 1,
   *   "orgCode": null
   * }
   */
  newEntity?: Record<string, any>
  /**
   * 字段结构描述（camelCase 字段名 → 类型信息）
   * 来源：后端反射实体自动生成
   * 用途：表单校验、控件类型推断
   */
  schema?: Record<string, EntityFieldSchema>
  // 以下为兼容旧版可选项
  configName?: string
  tableName?: string
  toolbar?: ToolbarConfigDto
  rowButtons?: RowButtonConfigDto
  searchFields?: SearchFieldConfig[]
}

/** 列配置 */
export interface ColumnConfig {
  fieldName: string
  desName: string
  width?: number
  type: string
  /** 是否在表格中显示 */
  xsFlag: boolean
  /** 是否在表单中显示和编辑（BCFlag=false 表示隐藏/只读） */
  bcFlag: boolean
  /** 是否允许空（空校验） */
  yxk: boolean
  /** 是否启用（控件是否可用） */
  enable?: boolean
  sortable?: boolean
  align?: string
  fixed?: string
  dictCode?: string
  format?: string
  /** 表单 Grid 布局：行号 */
  row?: number
  /** 表单 Grid 布局：列号 */
  col?: number
  /** 表单 Grid 布局：跨行数 */
  rowSpan?: number
  /** 表单 Grid 布局：跨列数 */
  colSpan?: number
  /** 默认值 */
  mrz?: any
  /** 列排序号（升序） */
  sxh?: number
}

/** 工具栏配置 */
export interface ToolbarConfigDto {
  add?: boolean
  delete?: boolean
  export?: boolean
  import?: boolean
  customButtons?: Record<string, string>  // key → label
}

/** 行按钮配置 */
export interface RowButtonConfigDto {
  edit?: boolean
  delete?: boolean
  customButtons?: Record<string, string>  // key → label
}

// ========================================================
// 六、左树右表完整配置
// ========================================================

/** 左树右表页面配置（后端返回） */
export interface TreeTableConfigDto {
  tableConfig: EntityConfigDto
  treeConfig: TreeBehaviorConfig
  /** 树节点表单配置（机构/目录等树节点编辑弹窗的字段定义） */
  treeFormConfig?: EntityConfigDto
}

// ========================================================
// 七、行操作（/action/{methodName}）
// ========================================================

/** 行操作请求 */
export interface RowActionRequest {
  /** 行数据实体 */
  entity: Record<string, any>
}

/** 行操作响应 */
export interface RowActionResponse {
  /** 操作是否成功 */
  success: boolean
  /** 结果消息 */
  message: string
  /** 返回数据（可选） */
  data?: any
}

// ========================================================
// 十、反射 Schema（V1）
// ========================================================

/**
 * 字段结构描述（后端反射生成）
 * 前端用于表单校验、控件类型推断
 */
export interface EntityFieldSchema {
  /** 字段类型：string/number/boolean/datetime */
  type: 'string' | 'number' | 'boolean' | 'datetime'
  /** 默认值（可直接用于初始化表单） */
  default: any
  /** 是否可选（允许为空） */
  optional: boolean
}
