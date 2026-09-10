/**
 * 前后端统一契约类型（V3 - 对齐 YZH.Core.Stand）
 *
 * 命名规范（与 YZH.Core.Stand 完全一致）：
 * ① ApiResponse 顶层字段：camelCase
 *    后端显式 [JsonPropertyName("success"/"data"/...)] 输出，
 *    前端按 camelCase 访问（success/data/message/code/timestamp）
 * ② 业务实体（DTO / EntityConfig / ColumnConfig / TreeItemDto / PagedResult...）：
 *    PascalCase（与 C# 属性、数据库列名一致，原样输出）
 * ③ 字典 key（EntityConfigDto.NewEntity / Schema）：
 *    camelCase（YZH.Core.Stand/EntitySchemaHelper 反射生成时显式 ToCamelCase）
 *    → 前端 formData / 业务数据 用 camelCase key，但访问业务实体字段用 PascalCase
 * ④ FilterRequest / FilterItem：PascalCase（与 YZH.Core.Stand/Request 一致）
 *
 * 不维护：
 * - 具体业务实体（Sys_User / Sys_Organization 等）→ 业务代码统一 any + NewEntity 兜底
 *
 * 关联文档：
 * - YZH.Core.Stand/Models/Result/ApiResponse.cs
 * - YZH.Core.Stand/Models/Request/PagedResult.cs
 * - YZH.Core.Stand/Models/Request/FilterRequest.cs
 * - YZH.Core.Stand/Models/Config/EntityConfigDto.cs
 * - YZH.Core.Stand/Models/Config/TreeConfig.cs
 * - YZH.Core.Stand/Helpers/EntitySchemaHelper.cs
 */

// ========================================================
// ① API 响应包装（顶层 camelCase，与 YZH.Core.Stand/ApiResponse 一致）
// ========================================================

export interface ApiResponse<T = any> {
  success: boolean
  message: string
  data: T
  code: number
  timestamp: string
}

/** 分页响应内联（Data 字段类型，YZH.Core.Stand/PagedResult） */
export interface PagedData<T = any> {
  Items: T[]
  TotalCount: number
  PageIndex: number
  PageSize: number
}

/** 兼容性别名 */
export type PagedResult<T = any> = PagedData<T>

// ========================================================
// ② 树节点 DTO（PascalCase，与 YZH.Core.Stand/TreeItemDto 一致）
// ========================================================

export interface TreeItemDto {
  Code: string
  Name: string
  ParentCode?: string
  NodeType?: string
  IsLeaf: boolean
  Level: number
  Sort?: number
  /** 扩展业务字段（enable/remark/leaderName 等通过此字典传递） */
  Extra?: Record<string, any>
  /** 子节点（全量加载场景） */
  Children?: TreeItemDto[]
}

/** 树懒加载请求（YZH.Core.Stand/TreeChildrenRequest） */
export interface TreeChildrenRequest {
  ParentCode: string
  Level: number
}

/** 树行为配置（YZH.Core.Stand/TreeBehaviorConfigDto） */
export interface TreeBehaviorConfig {
  Lazy: boolean
  AllowEdit: boolean
  AllowDelete: boolean
  AllowRename: boolean
  RootParentCode?: any
  NameField: string
  CodeField: string
  ParentCodeField: string
  RelateField: string
  NoSelectionBehavior: 'empty' | 'all'
  MaxLevel: number
  AllowDeleteWithChildren: boolean
  /** 自定义节点操作按钮：{ 方法名: 显示文字 } */
  CustomActions?: Record<string, string>
  /** 启用/禁用字段名（默认 IsValid） */
  EnableField?: string
}

// ========================================================
// ③ 过滤请求（PascalCase，与 YZH.Core.Stand/FilterRequest 一致）
// ========================================================

export interface FilterRequest {
  Page: number
  PageSize: number
  SortField?: string
  SortOrder?: 'asc' | 'desc'
  Filters: FilterItem[]
  ShowDisabled?: boolean
}

export interface FilterItem {
  Field: string
  Operator:
    | 'eq'
    | 'neq'
    | 'gt'
    | 'gte'
    | 'lt'
    | 'lte'
    | 'like'
    | 'in'
    | 'isnull'
    | 'isnotnull'
  Value?: any
}

// ========================================================
// ④ 实体配置（PascalCase，YZH.Core.Stand/EntityConfigDto）
// 字典 key（NewEntity/Schema）说明：key 是 camelCase（反射生成 ToCamelCase）
// ========================================================

export interface ColumnConfig {
  FieldName: string
  DesName: string
  Width?: number
  Type: string
  /** 是否在表格中显示 */
  XsFlag: boolean
  /** 是否在表单中显示和编辑 */
  BcFlag: boolean
  /** 是否允许空（空校验） */
  Yxk: boolean
  /** 控件是否可用 */
  Enable?: boolean
  Sortable?: boolean
  Align?: string
  Fixed?: string
  DictCode?: string
  Format?: string
  Row?: number
  Col?: number
  RowSpan?: number
  ColSpan?: number
  Mrz?: any
  /** 分组索引（编辑模式控制）：'0'=默认可编辑，'1'+=特定模式只读，'99'=详情全部只读 */
  GroupIndex?: string
}

export interface SearchFieldConfig {
  Label: string
  Field: string
  Operator?: string
  ControlType: string
  Options?: { Label: string; Value: string }[]
  Width?: number
}

export interface ToolbarConfig {
  Add?: boolean
  Delete?: boolean
  Export?: boolean
  Import?: boolean
  /** 自定义按钮：{ 按钮文字: 后端方法名 } */
  CustomButtons?: Record<string, string>
}

export interface RowButtonConfig {
  Edit?: boolean
  Delete?: boolean
  CustomButtons?: Record<string, string>
}

export interface EntityFieldSchema {
  Type: string
  Default?: any
  Optional: boolean
}

export interface EntityConfigDto {
  Title: string
  FillMode?: 'AutoFix' | 'PixFix'
  /** 表单布局列数（1=单列，2=双列，0=自动：BcFlag字段≤10用1列，>10用2列） */
  FormCols?: number
  Columns: ColumnConfig[]
  /**
   * 空实体模板：camelCase key（YZH.Core.Stand/EntitySchemaHelper 反射 ToCamelCase）
   * 例：{ code: "", userName: "", enable: 1, orgCode: "root" }
   */
  NewEntity?: Record<string, any>
  /**
   * 字段结构描述：camelCase key → 字段描述
   * 例：{ code: { type: "string", default: "", optional: false } }
   */
  Schema?: Record<string, EntityFieldSchema>
  /** 兼容旧字段（来自 JSON 文件） */
  ConfigName?: string
  TableName?: string
  /** 工具栏配置 */
  Toolbar?: ToolbarConfig
  /** 行按钮配置 */
  RowButtons?: RowButtonConfig
  /** 搜索字段配置 */
  SearchFields?: SearchFieldConfig[]
  /** 启用/禁用字段名（默认 IsValid） */
  EnableField?: string
}

/** 左树右表完整配置（YZH.Core.Stand/TreeTableConfigDto） */
export interface TreeTableConfigDto {
  TableConfig: EntityConfigDto
  TreeConfig: TreeBehaviorConfig
  TreeFormConfig?: EntityConfigDto
}
