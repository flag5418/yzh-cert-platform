/**
 * TreeTableLogic - 左树右表 Logic 基类（V2）
 *
 * 设计理念：
 * - V2 变化：去 T 泛型，弱化实体类型约束（T 由 entityToTreeNode 内部处理）
 * - 底层使用 treeOps 不可变变换
 * - 内置 ActionPipeline 支持 before/run/after 动作分发
 * - 支持同构树（单表）和异构树（多表组合）
 *
 * 使用方式：
 *   // index.ts
 *   export class DeptPageLogic extends TreeTableLogic<SysUser> {
 *     controllerName = 'SysDept'
 *     treeConfig = { ... }
 *     relation = { relateField: 'deptCode' }
 *
 *     // 覆盖 virtual 方法
 *     protected buildTreeFromItems(items: any[]): TreeNode[] {
 *       // 异构树构造逻辑
 *     }
 *   }
 *
 *   // index.vue
 *   const page = new DeptPageLogic()
 *   await page.init()
 */

import { reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type {
  TreeNode,
  TreeConfig,
  TreeNodeAction,
  ActionCtx,
  ActionPipeline,
  GetChildrenRequest,
  GetChildrenResponse
} from '../types/tree'
import { treeUtils } from '../utils/treeUtils'
import { treeOps as treeOpsTransform } from '../utils/treeOps'

// ========================================================
// 类型定义
// ========================================================

/**
 * 默认 Action Pipeline 实现
 */
export class DefaultActionPipeline<T> implements ActionPipeline<T> {
  async before(_action: TreeNodeAction, _ctx: ActionCtx<T>): Promise<boolean> {
    return true // 默认不中止
  }

  async run(action: TreeNodeAction, ctx: ActionCtx<T>): Promise<void> {
    // 默认实现为空，业务可覆盖
    console.log('[TreeTableLogic] action run:', action, ctx)
  }

  after(action: TreeNodeAction, ctx: ActionCtx<T>): void {
    // 默认实现为空
    console.log('[TreeTableLogic] action after:', action, ctx)
  }
}

// ========================================================
// TreeTableLogic 基类
// ========================================================

/**
 * 左树右表 Logic 基类
 *
 * @template V 表格实体类型（如 SysUser）
 */
export abstract class TreeTableLogic<V extends Record<string, any>> {
  // ──── 配置 ────

  /** 控制器名称 */
  abstract controllerName: string

  /** 树配置 */
  abstract treeConfig: TreeConfig

  /** 表格与树的关联配置 */
  abstract relation: { relateField: string }

  // ──── 树状态 ────

  /** 树数据 */
  treeData = ref<TreeNode[]>([])

  /** 树加载状态 */
  treeLoading = ref(false)

  /** 当前点击节点 */
  selectedNode = ref<TreeNode | null>(null)

  /** 勾选节点集合 */
  checkedNodes = ref<TreeNode[]>([])

  // ──── 表格状态 ────

  /** 表格数据 */
  rows = ref<V[]>([])

  /** 加载状态 */
  loading = ref(false)

  /** 分页 */
  pagination = reactive({ page: 1, pageSize: 20, total: 0 })

  /** 搜索参数 */
  searchParams = reactive<Record<string, any>>({})

  // ──── 动作管线 ────

  /** Action Pipeline（可覆盖自定义） */
  protected actionPipeline: ActionPipeline = new DefaultActionPipeline()

  // ========================================================
  // 抽象方法（子类必须实现）
  // ========================================================

  /**
   * 加载树数据
   * @param parentCode 父节点编码（懒加载时传入）
   * @returns 实体列表（扁平）
   */
  abstract loadTree(parentCode?: string | null): Promise<any[]>

  /**
   * 实体 → TreeNode 映射
   */
  abstract entityToTreeNode(entity: any, level: number): TreeNode

  /**
   * TreeNode → 实体（用于提交）
   */
  abstract treeNodeToEntity(node: TreeNode): Record<string, any>

  // ========================================================
  // Virtual 方法（子类可覆盖，用于复杂树构造）
  // ========================================================

  /**
   * ★ Virtual：构造树形结构
   *
   * 默认实现：调用 treeUtils.buildTree() 自动构造
   *
   * 子类可覆盖此方法实现：
   * - 多表组合树（如 机构+标准+阶段）
   * - 异构树（不同层级不同类型）
   * - 自定义过滤/排序逻辑
   */
  protected buildTreeFromItems(items: any[]): TreeNode[] {
    return treeUtils.buildTree(items, this.treeConfig)
  }

  /**
   * ★ Virtual：自定义节点转换
   *
   * 默认实现：调用 entityToTreeNode()
   */
  protected convertEntityToNode(entity: any, level: number): TreeNode {
    return this.entityToTreeNode(entity, level)
  }

  /**
   * ★ Virtual：加载后的数据处理
   */
  protected onTreeLoaded(treeData: TreeNode[]): void {
    if (this.treeConfig.onTreeLoaded) {
      this.treeConfig.onTreeLoaded(treeData)
    }
  }

  /**
   * ★ Virtual：推断子节点类型（异构树用）
   */
  protected inferChildNodeType(_node: TreeNode): string {
    return 'default'
  }

  // ========================================================
  // 初始化
  // ========================================================

  /**
   * 初始化（树 + 可选自动加载表格）
   */
  async init(autoLoadTable = false): Promise<void> {
    await this.initTree()
    if (autoLoadTable && this.treeData.value.length > 0) {
      // 自动选中第一个根节点
      const firstRoot = this.treeData.value[0]
      await this.onNodeClick(firstRoot)
    }
  }

  /**
   * 初始化树
   */
  async initTree(): Promise<void> {
    this.treeLoading.value = true
    try {
      if (this.treeConfig.lazy) {
        // 懒加载：只加载根节点
        const rootItems = await this.loadTree(this.treeConfig.rootParentCode)
        this.treeData.value = this.buildTreeFromItems(rootItems)
        // 为根节点设置 isLeaf（若无子节点信息）
        this.treeData.value.forEach(node => {
          if (node.isLeaf === undefined) {
            node.isLeaf = false // 默认非叶子，懒加载时由组件判断
          }
        })
      } else {
        // 全量加载
        const items = await this.loadTree()
        this.treeData.value = this.buildTreeFromItems(items)
      }
      this.onTreeLoaded(this.treeData.value)
    } finally {
      this.treeLoading.value = false
    }
  }

  // ========================================================
  // 懒加载
  // ========================================================

  /**
   * 懒加载子节点
   *
   * 注意：V2 中，懒加载状态由 el-tree/naive 组件内部管理
   * 此函数只负责加载数据并返回子节点数组
   */
  async loadChildren(parentNode: TreeNode): Promise<TreeNode[]> {
    const request: GetChildrenRequest = {
      parentCode: parentNode.code
    }

    const result = await this.apiPost<GetChildrenResponse>('/getChildren', request)

    return result.items.map(item => {
      const node = this.convertEntityToNode(item, 0)
      node.parentCode = parentNode.code
      return node
    })
  }

  // ========================================================
  // 节点点击 → 联动表格
  // ========================================================

  /**
   * 节点点击
   */
  async onNodeClick(node: TreeNode): Promise<void> {
    // 更新选中状态
    if (this.selectedNode.value) {
      // UI 状态由组件管理，这里只更新业务数据
    }

    this.selectedNode.value = node

    // 检查是否可选择
    if (this.treeConfig.onlyLeafSelectable && !node.isLeaf) {
      return
    }

    // 执行动作管线
    const ctx: ActionCtx = { node }
    const canProceed = await this.actionPipeline.before('select', ctx)
    if (!canProceed) return

    await this.actionPipeline.run('select', ctx)
    await this.loadDataWithTreeCondition(node)
    this.actionPipeline.after('select', ctx)
  }

  /**
   * 节点勾选变更（仅业务数据层面）
   */
  onCheckChange(node: TreeNode, checked: boolean): void {
    const ctx: ActionCtx = { node, payload: { checked } }
    this.actionPipeline.before('check', ctx)
    // 勾选逻辑由组件内部处理（el-tree/naive）
    this.actionPipeline.after('check', ctx)
    this.updateCheckedNodes()
  }

  /**
   * 更新勾选节点集合
   */
  private updateCheckedNodes(): void {
    // 从组件获取勾选数据（组件实现桥接）
    // 此处为占位，实际由 YzhTree 组件回调
  }

  // ========================================================
  // 表格查询
  // ========================================================

  /**
   * 带树条件的分页查询
   */
  async loadDataWithTreeCondition(node: TreeNode): Promise<void> {
    this.loading.value = true
    try {
      const conditions = {
        ...this.buildConditions(),
        [this.relateFieldToCondition()]: node.code
      }
      const res = await this.apiPost<{ rows: V[]; total: number }>('/page', {
        page: this.pagination.page,
        rows: this.pagination.pageSize,
        sort: undefined,
        order: undefined,
        conditions
      })
      this.rows.value = res.rows
      this.pagination.total = res.total
    } finally {
      this.loading.value = false
    }
  }

  /**
   * 普通分页查询（无树条件）
   */
  async loadData(): Promise<void> {
    this.loading.value = true
    try {
      const res = await this.apiPost<{ rows: V[]; total: number }>('/page', {
        page: this.pagination.page,
        rows: this.pagination.pageSize,
        sort: undefined,
        order: undefined,
        conditions: this.buildConditions()
      })
      this.rows.value = res.rows
      this.pagination.total = res.total
    } finally {
      this.loading.value = false
    }
  }

  // ========================================================
  // 树节点 CRUD（增量更新）
  // ========================================================

  /**
   * 新增节点
   */
  async addTreeNode(parentNode: TreeNode | null, entity: Record<string, any>): Promise<TreeNode | null> {
    const ctx: ActionCtx = { parent: parentNode ?? undefined, payload: entity }

    const canProceed = await this.actionPipeline.before('add', ctx)
    if (!canProceed) return null

    await this.actionPipeline.run('add', ctx)

    // 设置父编码
    const data = {
      ...entity,
      [this.treeConfig.parentCodeField]: parentNode?.code ?? this.treeConfig.rootParentCode
    }

    // 后端保存
    const saved = await this.apiPost<Record<string, any>>('/add', data)

    // 构造新节点
    const newNode = this.convertEntityToNode(saved, (parentNode?.extra?.level ?? -1) + 1)
    newNode.parentCode = parentNode?.code ?? this.treeConfig.rootParentCode

    // 增量更新树（使用 treeOps 不可变变换）
    this.treeData.value = treeOpsTransform.addNode(this.treeData.value, parentNode?.code ?? null, newNode)

    this.actionPipeline.after('add', ctx)

    ElMessage.success('创建成功')
    return newNode
  }

  /**
   * 修改节点
   */
  async updateTreeNode(node: TreeNode, newName: string): Promise<void> {
    const ctx: ActionCtx = { node, payload: { newName } }

    const canProceed = await this.actionPipeline.before('edit', ctx)
    if (!canProceed) return

    await this.actionPipeline.run('edit', ctx)

    const entity = this.treeNodeToEntity(node)
    entity[this.treeConfig.nameField] = newName
    await this.apiPost('/update', entity)

    // 增量更新树
    this.treeData.value = treeOpsTransform.updateNode(this.treeData.value, node.code, { name: newName })

    this.actionPipeline.after('edit', ctx)

    ElMessage.success('修改成功')
  }

  /**
   * 删除节点
   */
  async deleteTreeNode(node: TreeNode): Promise<boolean> {
    // 删除策略检查
    if (!this.treeConfig.allowDeleteWithChildren && node.children.length > 0) {
      ElMessage.warning('该节点包含子节点，无法删除')
      return false
    }

    const ctx: ActionCtx = { node }

    const canProceed = await this.actionPipeline.before('delete', ctx)
    if (!canProceed) return false

    await this.actionPipeline.run('delete', ctx)

    // 递归删除所有子孙的 code
    const descendants = treeUtils.getDescendantsWithSelf(node)
    const codes = descendants.map(n => n.code)

    await this.apiPost('/delete', codes)

    // 增量更新树
    const { tree } = treeOpsTransform.removeSubtree(this.treeData.value, node.code)
    this.treeData.value = tree

    // 如果当前选中节点被清除
    if (this.selectedNode.value?.code === node.code) {
      this.selectedNode.value = null
    }

    this.actionPipeline.after('delete', ctx)

    ElMessage.success('删除成功')
    return true
  }

  /**
   * 批量删除（根据勾选）
   */
  async batchDelete(codes: string[]): Promise<boolean> {
    if (codes.length === 0) {
      ElMessage.warning('请先选择要删除的节点')
      return false
    }

    // TODO: 确认对话框
    // await ElMessageBox.confirm(`确定删除 ${codes.length} 个节点？`, '删除确认')

    await this.apiPost('/delete', codes)

    // 逐个删除（treeOps.removeSubtree 会处理）
    let currentTree = this.treeData.value
    for (const code of codes) {
      const result = treeOpsTransform.removeSubtree(currentTree, code)
      currentTree = result.tree
    }
    this.treeData.value = currentTree

    this.checkedNodes.value = []
    ElMessage.success(`已删除 ${codes.length} 个节点`)
    return true
  }

  // ========================================================
  // 树操作辅助方法
  // ========================================================

  /**
   * 获取勾选节点的 code[]
   */
  getCheckedCodes(): string[] {
    return this.checkedNodes.value.map(n => n.code)
  }

  /**
   * 刷新树（重新加载）
   */
  async refreshTree(): Promise<void> {
    await this.initTree()
  }

  /**
   * 刷新当前节点下的子节点（懒加载场景）
   */
  async refreshChildren(node: TreeNode): Promise<void> {
    if (!this.treeConfig.lazy) {
      await this.refreshTree()
      return
    }

    // 加载子节点
    const children = await this.loadChildren(node)
    this.treeData.value = treeOpsTransform.updateNode(
      this.treeData.value,
      node.code,
      { children, isLeaf: children.length === 0 }
    )
  }

  /**
   * 移动节点
   */
  async moveTreeNode(node: TreeNode, newParentCode: string | null): Promise<boolean> {
    const ctx: ActionCtx = { node, payload: { newParentCode } }

    const canProceed = await this.actionPipeline.before('move', ctx)
    if (!canProceed) return false

    // 调用 treeOps 内置防环/防深度校验
    const result = treeOpsTransform.moveSubtree(
      this.treeData.value,
      node.code,
      newParentCode,
      this.treeConfig.maxLevel
    )

    if (result.error) {
      ElMessage.error(result.error)
      return false
    }

    this.treeData.value = result.tree

    // 后端更新 parentCode
    const entity = this.treeNodeToEntity(node)
    entity[this.treeConfig.parentCodeField] = newParentCode
    await this.apiPost('/update', entity)

    this.actionPipeline.after('move', ctx)

    ElMessage.success('移动成功')
    return true
  }

  // ========================================================
  // 内部辅助方法
  // ========================================================

  /** 表格查询条件字段名（camelCase → snake_case） */
  private relateFieldToCondition(): string {
    return this.relation.relateField
      .replace(/([A-Z])/g, '_$1')
      .toLowerCase()
  }

  /** 构建查询条件 */
  protected buildConditions(): any[] {
    return Object.entries(this.searchParams)
      .filter(([, v]) => v != null && v !== '' && !(Array.isArray(v) && v.length === 0))
      .map(([field, value]) => ({ field, value, operator: 'eq' }))
  }

  /** API POST */
  protected async apiPost<R = any>(path: string, body?: any): Promise<R> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.post<R>(url, body)
  }

  /** API GET */
  protected async apiGet<R = any>(path: string, params?: any): Promise<R> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.get<R>(url, params)
  }
}

// ========================================================
// 导出
// ========================================================

export default TreeTableLogic
