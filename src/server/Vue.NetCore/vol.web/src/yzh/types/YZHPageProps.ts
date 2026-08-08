// ============================================================
//  YZH Framework V2.5 —— 基类 Props 类型（支持配置驱动）
//
//  V2.5 新增：
//  - pageKey prop：从后端 yzh_page_config + yzh_field_config 加载 UI 配置
//  - control_type 完整语义体系
// ============================================================

import type { IYZHEntitySchema } from './YZHEntitySchema'
import type { IYZHPageLifecycle } from './YZHLifecycles'

/** 工具栏按钮开关 */
export interface IYZHButtons {
  add: boolean
  refresh: boolean
  import: boolean
  export: boolean
  column: boolean
  editMode: boolean
  batchDelete: boolean
  sort: boolean
}

/** 外部过滤条件（左树右表场景） */
export interface IYZHExternalFilter {
  name: string
  value: any
  cond?: '==' | '!=' | '>' | '<' | '>=' | '<=' | 'like' | 'contains'
}

/**
 * control_type 录入类型完整语义
 *
 * | 类型        | 弹窗显示 | 参与保存 | 典型场景                     |
 * |-------------|---------|---------|----------------------------|
 * | input       | ✓ 文本框| ✓       | 普通文本录入                 |
 * | select      | ✓ 下拉框| ✓       | 字典选择                     |
 * | textarea    | ✓ 多行  | ✓       | 长文本备注                   |
 * | number      | ✓ 数字  | ✓       | 数值/年份                    |
 * | date        | ✓ 日期  | ✓       | 日期选择                     |
 * | switch      | ✓ 开关  | ✓       | 布尔切换                     |
 * | hidden      | ✗ 隐藏  | ✓ 提交  | Id, 外键(自动填充), 系统字段  |
 * | readonly    | ✓ 只读  | ✗       | 创建人/创建时间(后端填充)     |
 * | none/other  | ✗ 隐藏  | ✗       | 纯计算字段/虚拟字段           |
 */
export type YzhControlType =
  | 'input' | 'select' | 'textarea' | 'number' | 'decimal'
  | 'date' | 'datetime' | 'switch' | 'radio' | 'checkbox'
  | 'hidden' | 'readonly' | 'none' | 'other'

/** 单表基类 Props */
export interface IYZHCrudTableProps<TKey, TEntity> {
  /** （必填）实体 Schema */
  schema: IYZHEntitySchema<TKey, TEntity>
  /**
   * （条件必填）页面配置（原 options.js 输出格式，兼容 Vol 配置结构）
   *
   * 配置驱动模式（传入 pageKey 时）：
   *   - 此项变为可选，作为回退/补充
   *   - columns / editFormOptions / searchFormOptions 可以为空数组
   *   - table 元数据（name/url/key）仍从此处读取
   *
   * 传统模式（不传 pageKey 时）：
   *   - 此项为必填，行为与之前完全一致
   */
  options?: (() => any) | any
  /** （可选 V2.5）页面唯一标识，对应 yzh_page_config.page_key
   *
   *  传入后自动从后端 API (/api/yzh-page-config/{pageKey}) 加载：
   *  - 页面元数据（弹窗宽度、标签宽度等）
   *  - 表格列配置（从 yzh_field_config 中 xs_flag=1 的字段生成）
   *  - 编辑表单配置（从 yzh_field_config 中 control_type 不是 none 的字段生成）
   *  - 搜索区配置（从 yzh_field_config 中 search_flag=1 的字段生成）
   *
   *  加载的配置会合并（覆盖）options.js 中的同名属性
   */
  pageKey?: string
  /** （可选）业务生命周期钩子 */
  lifecycles?: Partial<IYZHPageLifecycle<TKey, TEntity>>
  /** 是否启用增量刷新（默认 true） */
  incrementalUpdate?: boolean
  /** 按钮开关 */
  buttons?: Partial<IYZHButtons>
  /** 搜索区模式：fixed=默认展开；togglable=可折叠 */
  searchMode?: 'fixed' | 'togglable' | 'hidden'
  /** 外部过滤条件 */
  externalFilter?: IYZHExternalFilter[]
  /** 是否显示操作列（默认 true） */
  showActionColumn?: boolean
  /** 弹窗宽度（默认 960px）— 可被 pageKey 配置覆盖 */
  dialogWidth?: number | string
}

/** 左树右表 Props（V2.1 → V2.5） */
export interface IYZHTreeTableProps<TKey, TEntity> extends Omit<IYZHCrudTableProps<TKey, TEntity>, 'pageKey'> {
  // ====== 必填 ======
  /** 左侧树数据对应的 Controller 名称（用于自动加载树数据） */
  treeControllerName: string
  /** 右侧表格的过滤字段名（如 CbCode，左侧选中节点的 key 会作为此字段的过滤值） */
  filterField: string

  // ====== 可选：树配置 ======
  /** 树节点显示的字段（默认 'Name'） */
  treeLabelField?: string
  /** 树节点 key 字段（默认 'Code' 或 'Id'） */
  treeKeyField?: string
  /** 树子节点字段名（默认 'children'） */
  treeChildrenField?: string
  /** 树面板标题（默认 '导航'） */
  treeTitle?: string
  /** 树面板宽度（默认 '240px'） */
  treeWidth?: string
  /** 是否显示刷新按钮（默认 true） */
  showTreeRefresh?: boolean
  /** 是否默认展开所有节点（默认 true） */
  defaultExpandAll?: boolean
  /** 是否显示节点计数 Badge（需要后端返回 _count 字段，默认 false） */
  showNodeCount?: boolean

  // ====== 可选：外部数据 ======
  /** 外部直接传入树数据（优先于 treeControllerName） */
  treeData?: any[]
  /** 树数据加载接口 URL（优先于 treeControllerName） */
  treeUrl?: string

  // ====== V2.5 配置驱动 ======
  /** （可选 V2.5）透传给内部 YzhCrudTable 的 pageKey */
  pageKey?: string
}
