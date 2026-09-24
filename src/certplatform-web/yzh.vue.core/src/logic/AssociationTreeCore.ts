/**
 * AssociationTreeCore - 关联型内核基类（AS-1）
 *
 * 语义：左树选择 + 右侧关联态维护（非 CRUD）。
 * - 左树：角色树（懒加载，API 注入）
 * - 右侧：被关联实体（勾选树 / 勾选表格）
 * - 编排：初始化缓存 → 选择节点 → 加载关联态 → 增量保存 + 乐观更新/回滚
 * - badge：数量来自本地关联缓存（局部更新，不重取全表）
 *
 * 差异通过注入 AssociationApi 与覆盖钩子表达。
 */

import { reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { TreeNode } from '../types/tree'
import type { AssociationSelection } from '../types/association'

// AssociationSelection 定义上移 types/association.ts（与 CheckTreeNode 等同源）；
// 此处保留再导出，维持既有 `import type { AssociationSelection } from './AssociationTreeCore'` 消费者兼容。
export type { AssociationSelection } from '../types/association'

/** 关联型 API 约定（由页面/api 模块注入，内核不拼端点） */
export interface AssociationApi {
  /** 左树根节点 */
  getTreeRoot: () => Promise<TreeNode[]>
  /** 左树子节点（懒加载） */
  getTreeChildren: (parentCode: string, level?: number) => Promise<TreeNode[]>
  /** 某节点的当前关联态（含 CheckFlag） */
  getAssociations: (nodeCode: string) => Promise<any[]>
  /** 新增关联；返回 Applied = 实际生效的完整 code 集合（服务端可能补全祖先） */
  add: (
    nodeCode: string,
    selections: AssociationSelection[],
  ) => Promise<{ Updated: number; Applied?: string[] }>
  /** 取消关联 */
  remove: (
    nodeCode: string,
    selections: AssociationSelection[],
  ) => Promise<{ Updated: number }>
  /** 全量关联（本地缓存初始化用） */
  getAll: () => Promise<Array<{ ContextCode: string; TargetCode: string }>>
}

export abstract class AssociationTreeCore {
  // ──── 左树 ────
  treeData = ref<TreeNode[]>([])
  selectedNode = ref<TreeNode | null>(null)

  // ──── 右侧关联数据 ────
  associationData = ref<any[]>([])

  // ──── 加载状态 ────
  loading = ref(false)
  saving = ref(false)

  // ──── 本地关联缓存（badge / 差集保存依据） ────
  protected associationCache = reactive<Map<string, Set<string>>>(new Map())
  protected cacheLoaded = false

  // ──── API 注入 ────
  protected api: AssociationApi

  constructor(api: AssociationApi) {
    this.api = api
  }

  // ========================================================
  // 初始化：加载本地缓存 + 左树
  // ========================================================

  async init(): Promise<void> {
    await this.initCache()
    await this.loadTreeRoot()
  }

  async initCache(): Promise<void> {
    if (this.cacheLoaded) return
    try {
      const associations = await this.api.getAll()
      this.buildCache(associations)
      this.cacheLoaded = true
    } catch (e: any) {
      console.error(`[${this.constructor.name}] 加载关联缓存失败:`, e)
    }
  }

  protected buildCache(
    associations: Array<{ ContextCode: string; TargetCode: string }>,
  ): void {
    this.associationCache.clear()
    for (const item of associations) {
      if (!this.associationCache.has(item.ContextCode)) {
        this.associationCache.set(item.ContextCode, new Set())
      }
      this.associationCache.get(item.ContextCode)!.add(item.TargetCode)
    }
  }

  // ========================================================
  // 左树
  // ========================================================

  async loadTreeRoot(): Promise<TreeNode[]> {
    try {
      const items = await this.api.getTreeRoot()
      this.treeData.value = items
      this.applyBadgesDeep(items)
      return items
    } catch (e: any) {
      ElMessage.error(e.message || '加载树失败')
      return []
    }
  }

  async loadChildren(
    node: any,
    resolve: (data: TreeNode[]) => void,
  ): Promise<void> {
    try {
      const items = await this.api.getTreeChildren(node.data.Code, node.level ?? 0)
      this.applyBadgesDeep(items)
      resolve(items)
    } catch (e: any) {
      ElMessage.error(e.message || '加载子节点失败')
      resolve([])
    }
  }

  // ========================================================
  // Badge（来自本地缓存，局部更新）
  // ========================================================

  getCountForNode(nodeCode: string): number {
    return this.associationCache.get(nodeCode)?.size ?? 0
  }

  getNodeBadge(nodeCode: string): string | undefined {
    const count = this.getCountForNode(nodeCode)
    return count > 0 ? String(count) : undefined
  }

  /** 写入/清除单节点 Extra.badge（原地改 → 仅该节点重渲染，el-tree 不重置展开/懒加载态） */
  protected applyBadge(node: TreeNode): void {
    const count = this.getCountForNode(node.Code)
    const extra = { ...(node.Extra ?? {}) }
    if (count > 0) {
      extra.badge = String(count)
    } else {
      delete extra.badge
    }
    node.Extra = extra
  }

  /** 递归注入徽标（树根 / 懒加载子节点 resolve 前调用） */
  protected applyBadgesDeep(nodes: TreeNode[]): void {
    for (const node of nodes) {
      this.applyBadge(node)
      if (node.Children?.length) this.applyBadgesDeep(node.Children)
    }
  }

  /** 按 Code 递归查找（懒加载子节点不在 treeData 时返回 null） */
  protected findNodeByCode(nodes: TreeNode[], code: string): TreeNode | null {
    for (const node of nodes) {
      if (String(node.Code) === String(code)) return node
      if (node.Children?.length) {
        const hit = this.findNodeByCode(node.Children, code)
        if (hit) return hit
      }
    }
    return null
  }

  /** 局部刷新单个节点徽标；找不到（如懒加载子节点未挂进 treeData）静默跳过，不退回整树替换 */
  protected refreshBadge(nodeCode?: string | null): void {
    if (!nodeCode) return
    const node = this.findNodeByCode(this.treeData.value, nodeCode)
    if (node) this.applyBadge(node)
  }

  // ========================================================
  // 节点选择 → 加载关联态
  // ========================================================

  async handleNodeSelect(node: TreeNode): Promise<void> {
    if (!node || !node.Code) return

    this.selectedNode.value = node
    this.loading.value = true

    try {
      const data = await this.api.getAssociations(node.Code)

      // 将 Extra 平铺到节点
      for (const item of data) {
        if (item.Extra) Object.assign(item, item.Extra)
      }

      // 用本地缓存覆盖勾选态
      const cached = this.associationCache.get(node.Code) ?? new Set<string>()
      for (const item of data) {
        item.CheckFlag = cached.has(item.Code)
      }

      // 子类后处理（如分组跟随逻辑）
      this.afterAssociationsLoaded(data, cached)

      this.associationData.value = data
    } catch (e: any) {
      ElMessage.error(e.message || '加载数据失败')
      this.associationData.value = []
    } finally {
      this.loading.value = false
    }
  }

  /** 子类覆盖：关联态加载后处理（如 role-api 的分组跟随） */
  protected afterAssociationsLoaded(_data: any[], _cached: Set<string>): void {}

  // ========================================================
  // 增量保存（乐观更新：先本地后端，失败提示）
  // ========================================================

  async handleCheckChange(payload: { added: string[]; removed: string[] }): Promise<void> {
    if (!this.selectedNode.value) {
      ElMessage.warning('请先选择左侧节点')
      return
    }

    const nodeCode = this.selectedNode.value.Code
    this.saving.value = true

    try {
      if (payload.added.length > 0) {
        const selections = this.buildSelections(payload.added)
        if (selections.length > 0) {
          const res = await this.api.add(nodeCode, selections)
          // 用服务端回传的 Applied（实际生效集合，含其自动补全的祖先）局部更新缓存
          this.syncCacheAdd(nodeCode, res.Applied ?? selections.map((s) => s.Code))
        }
      }

      if (payload.removed.length > 0) {
        const selections = this.buildSelections(payload.removed)
        if (selections.length > 0) {
          await this.api.remove(nodeCode, selections)
          this.syncCacheRemove(nodeCode, selections.map((s) => s.Code))
        }
      }

      ElMessage.success('保存成功')
    } catch (e: any) {
      ElMessage.error(e.message || '保存失败')
    } finally {
      // 缓存可能已部分更新（如 add 成功 remove 失败）→ 以缓存现状刷新该节点徽标
      this.refreshBadge(nodeCode)
      this.saving.value = false
    }
  }

  protected syncCacheAdd(nodeCode: string, codes: string[]): void {
    let cache = this.associationCache.get(nodeCode)
    if (!cache) {
      cache = new Set<string>()
      this.associationCache.set(nodeCode, cache)
    }
    for (const code of codes) cache.add(code)
  }

  protected syncCacheRemove(nodeCode: string, codes: string[]): void {
    const cache = this.associationCache.get(nodeCode)
    if (!cache) return
    for (const code of codes) cache.delete(code)
  }

  // ========================================================
  // 选择项构建（子类定义可勾选的 NodeType）
  // ========================================================

  /** 子类定义：哪些 NodeType 可被勾选（空数组 = 全部） */
  protected get selectableNodeTypes(): string[] {
    return []
  }

  protected buildSelections(codes: string[]): AssociationSelection[] {
    const selections: AssociationSelection[] = []
    const allNodes = this.associationData.value

    for (const code of codes) {
      const node = allNodes.find((n) => n.Code === code)
      if (
        node &&
        (this.selectableNodeTypes.length === 0 ||
          this.selectableNodeTypes.includes(node.NodeType))
      ) {
        selections.push({ Code: code, NodeType: node.NodeType })
      }
    }

    return selections
  }
}

export default AssociationTreeCore
