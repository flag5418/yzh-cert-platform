/**
 * 树形结构类型定义（V2）
 *
 * 设计原则：
 * 1. TreeNode 只承载业务数据 + 结构元数据，不含 UI 状态
 * 2. UI 状态由组件/视图层持有（UiTreeNode 扩展类型）
 * 3. treeUtils 只读查询，treeOps 不可变变换
 *
 * 与 V1 的核心区别：
 * - 去掉了 id，统一 code/parentCode
 * - 去掉了 isExpanded/checked/isLoading 等 UI 状态
 * - 泛型支持 TreeNode<T>
 * - lazyLoadConfig 移入 TreeConfig，不进节点
 */

// ========================================================
// 一、核心 TreeNode（业务数据 + 结构元数据，无 UI 状态）
// ========================================================

/**
 * 前端标准树节点（虚拟结构）
 *
 * ★ 核心原则：
 * - 去掉 id，所有关联统一用 code
 * - 只承载业务数据 + 结构元数据
 * - UI 状态由组件持有，不存入此结构
 *
 * 无论后端 T 实体字段如何，一律映射到此结构
 */
export interface TreeNode<T = any> {
  // ──── 核心标识 ────
  /** 业务编码（唯一标识，关联查询用） */
  code: string
  /** 显示名称 */
  name: string
  /** 父节点编码（根节点为空字符串或 null） */
  parentCode: string | null

  // ──── 结构元数据（由后端或工具函数计算） ────
  /** 节点类型（异构树区分来源，同构树可省略） */
  nodeType?: string
  /** 是否有子级（后端事实，用于组件 leaf 预判） */
  isLeaf?: boolean
  /** 排序号 */
  sort?: number
  /** 附加业务字段（图标/颜色/badge/业务键值） */
  extra?: Record<string, any>

  // ──── 层级结构 ────
  /** 子节点集合 */
  children: TreeNode<T>[]

  // ──── 原始数据引用 ────
  /** 原始 T 实体完整数据（可选，仅需要提交时携带） */
  raw?: T
}

// ========================================================
// 二、视图层扩展类型（UI 状态，不进核心模型）
// ========================================================

/**
 * UI 状态扩展（由组件/视图层持有）
 *
 * 使用场景：
 * - el-tree 的选中/勾选状态
 * - 组件内部的加载/展开状态
 *
 * 示例：
 *   const uiNode: UiTreeNode<MyEntity> = {
 *     ...treeNode,
 *     checked: true,
 *     isExpanded: false,
 *     isLoading: false
 *   }
 */
export type UiTreeNode<T = any> = TreeNode<T> & {
  checked?: boolean
  indeterminate?: boolean
  selected?: boolean
  isExpanded?: boolean
  isLoading?: boolean
}

// ========================================================
// 三、树配置
// ========================================================

/**
 * 树结构字段映射配置
 *
 * 将后端 T 实体字段映射到 TreeNode 标准字段
 */
export interface TreeConfig<T = any> {
  // ──── 字段映射（必填） ────
  /** 业务编码字段 → TreeNode.code */
  codeField: string
  /** 显示名称字段 → TreeNode.name */
  nameField: string
  /** 父节点编码字段 → TreeNode.parentCode */
  parentCodeField: string

  // ──── 字段映射（可选） ────
  /** 节点类型字段 → TreeNode.nodeType */
  typeField?: string
  /** 是否叶子字段 → TreeNode.isLeaf */
  leafField?: string
  /** 排序字段 → TreeNode.sort */
  sortField?: string
  /** 扩展字段 → TreeNode.extra */
  extraFields?: string[]

  // ──── 行为配置 ────
  /** 根节点父编码值（加载根节点条件，如 null, '', 'root'） */
  rootParentCode: string | null
  /** 是否懒加载 */
  lazy: boolean
  /** 关联字段（表格查询时使用） */
  relateField: string
  /** 是否严格模式（勾选不联动父子，仅组件行为） */
  checkStrictly?: boolean
  /** 是否只能选择叶子节点（仅组件行为） */
  onlyLeafSelectable?: boolean
  /** 是否允许删除含子节点的父节点 */
  allowDeleteWithChildren: boolean
  /** 最大层级深度（0=不限） */
  maxLevel?: number

  // ──── 自定义转换钩子 ────
  /** 自定义实体到 TreeNode 的转换（覆盖默认行为） */
  entityToNode?: (entity: T, level: number) => Partial<TreeNode<T>>
  /** 加载后处理钩子 */
  onTreeLoaded?: (treeData: TreeNode<T>[]) => void
}

// ========================================================
// 四、节点类型系统（用于异构树类型约束）
// ========================================================

/**
 * 节点操作类型
 */
export type TreeNodeAction = 'add' | 'edit' | 'delete' | 'select' | 'check' | 'move' | 'export'

/**
 * 节点类型定义
 *
 * 不同类型节点：
 * - 显示不同图标
 * - 点击时右侧展示不同信息
 * - 可用操作不同
 * - 可包含的子节点类型不同
 */
export interface NodeTypeConfig {
  /** 类型标识 */
  type: string
  /** 显示名称 */
  label: string
  /** 图标 */
  icon: string
  /** 允许的操作 */
  allowedActions: TreeNodeAction[]
  /** 允许的子节点类型（空=不允许子节点） */
  allowedChildTypes: string[]
  /** 是否可作为关联目标（右侧表格查询条件） */
  isRelatable: boolean
  /** 是否只能作为根节点 */
  onlyRoot: boolean
  /** 是否只能作为叶子节点 */
  onlyLeaf: boolean
  /** 最大子节点数（0=不限） */
  maxChildren: number
  /** 节点说明 */
  description?: string
}

// ========================================================
// 五、Action Pipeline（动作分发管线）
// ========================================================

/**
 * 动作上下文
 */
export interface ActionCtx<T = any> {
  /** 当前节点 */
  node?: TreeNode<T>
  /** 父节点 */
  parent?: TreeNode<T>
  /** 附加数据 */
  payload?: any
  /** 取消标志 */
  cancelled?: boolean
}

/**
 * Action Pipeline 接口
 *
 * 用于标准化节点操作的分发和联动：
 * - before: 前置处理（返回 false 中止）
 * - run: 实际执行（默认实现/业务覆盖）
 * - after: 后置联动（刷新/通知等）
 */
export interface ActionPipeline<T = any> {
  before(action: TreeNodeAction, ctx: ActionCtx<T>): Promise<boolean> | boolean
  run(action: TreeNodeAction, ctx: ActionCtx<T>): Promise<void>
  after(action: TreeNodeAction, ctx: ActionCtx<T>): void
}

// ========================================================
// 六、LazyLoad 配置（TreeConfig 级别，非节点级别）
// ========================================================

/**
 * 懒加载配置（TreeConfig 级别）
 *
 * 注意：V2 将懒加载配置从节点移入 TreeConfig
 */
export interface LazyLoadConfig {
  /** 是否启用懒加载 */
  enabled: true
  /** 是否分页加载 */
  needPagination?: boolean
  /** 每页条数 */
  pageSize?: number
}

// ========================================================
// 七、树差异（用于增量同步）
// ========================================================

/**
 * 树差异补丁
 */
export interface TreePatch<T = any> {
  /** 新增的节点 */
  added: TreeNode<T>[]
  /** 删除的节点 code */
  removed: string[]
  /** 更新的节点 */
  updated: Array<{ code: string; changes: Partial<TreeNode<T>> }>
}

// ========================================================
// 八、后端树节点接口
// ========================================================

/**
 * 后端树节点 DTO（接口响应项）
 */
export interface TreeItemDto {
  code: string
  name: string
  parentCode: string | null
  nodeType?: string
  isLeaf?: boolean
  sort?: number
  extra?: Record<string, any>
  /** 子节点（全量加载时内嵌） */
  children?: TreeItemDto[]
}

/**
 * 懒加载请求参数
 */
export interface GetChildrenRequest {
  /** 父节点编码 */
  parentCode: string
  /** 页码（分页懒加载时） */
  page?: number
  /** 每页条数 */
  pageSize?: number
}

/**
 * getChildren 响应格式
 */
export interface GetChildrenResponse {
  items: TreeItemDto[]
  hasMore?: boolean
}

// ========================================================
// 九、常用节点类型定义示例（导出供参考）
// ========================================================

/** 组织机构节点类型定义示例 */
export const OrgNodeTypeExamples: NodeTypeConfig[] = [
  {
    type: 'root',
    label: '根机构',
    icon: 'OfficeBuilding',
    allowedActions: ['add'],
    allowedChildTypes: ['company'],
    isRelatable: false,
    onlyRoot: true,
    onlyLeaf: false,
    maxChildren: 1
  },
  {
    type: 'company',
    label: '公司',
    icon: 'Company',
    allowedActions: ['add', 'edit', 'delete', 'select'],
    allowedChildTypes: ['company', 'department'],
    isRelatable: true,
    onlyRoot: false,
    onlyLeaf: false,
    maxChildren: 0
  },
  {
    type: 'department',
    label: '部门',
    icon: 'User',
    allowedActions: ['add', 'edit', 'delete', 'select'],
    allowedChildTypes: ['department', 'team'],
    isRelatable: true,
    onlyRoot: false,
    onlyLeaf: false,
    maxChildren: 0
  },
  {
    type: 'team',
    label: '小组',
    icon: 'Avatar',
    allowedActions: ['edit', 'delete', 'select'],
    allowedChildTypes: [],
    isRelatable: true,
    onlyRoot: false,
    onlyLeaf: true,
    maxChildren: 0
  }
]
