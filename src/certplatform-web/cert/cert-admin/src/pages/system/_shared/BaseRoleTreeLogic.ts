/**
 * BaseRoleTreeLogic - 角色-树关联管理基类
 *
 * 三个子类共享 90% 逻辑：
 * - RoleUserLogic（角色-用户）
 * - RoleMenuLogic（角色-菜单）
 * - RoleApiLogic（角色-接口）
 *
 * 差异点通过子类覆写：
 * - loadRoleTreeRoot / loadRoleTreeChildren（角色树 API 不同）
 * - buildSelections（过滤的 NodeType 不同）
 * - handleRoleSelectPostProcess（role-api 有分组跟随逻辑）
 * - columns（表格列不同）
 */

import { ref, reactive } from 'vue'
import { ElMessage } from 'element-plus'
import type { CheckTreeNode, TreeNodeSelection, AssociationDto, RoleTreeItem } from '@share/types'

// 三个子类（role-api / role-menu / role-user）统一从本模块取这两个类型，
// 这里显式再导出，避免子类从「只 import 未 export」的模块导入而报 TS2459。
export type { CheckTreeNode, RoleTreeItem } from '@share/types'

/** 角色树节点 */
export interface RoleTreeNode {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType: string
  IsLeaf: boolean
  Extra?: Record<string, any>
  children: RoleTreeNode[]
}

/** API 接口定义（注入到构造函数） */
export interface RoleTreeApi {
  getRoleTreeRoot: () => Promise<RoleTreeItem[]>
  getRoleTreeChildren: (parentCode: string, level?: number) => Promise<RoleTreeItem[]>
  getCheckTree: (roleCode: string) => Promise<CheckTreeNode[]>
  /**
   * 新增授权
   *
   * 返回值 `Applied` = 本次调用后「应当已授权」的完整 code 集合。
   * 角色-菜单场景下服务端还会自动补全祖先菜单，这部分码前端推不出来，
   * 所以由服务端回传 —— 前端据此局部更新缓存，无需再全量重取。
   */
  checkAdd: (
    roleCode: string,
    selections: TreeNodeSelection[],
  ) => Promise<{ Updated: number; Applied?: string[] }>
  checkRemove: (roleCode: string, selections: TreeNodeSelection[]) => Promise<{ Updated: number }>
  getAllAssociations: () => Promise<AssociationDto[]>
}

export abstract class BaseRoleTreeLogic {
  // ──── 左侧角色树 ────
  roleTreeData = ref<RoleTreeItem[]>([])
  selectedRole = ref<RoleTreeItem | null>(null)

  // ──── 右侧混合树数据 ────
  checkTreeData = ref<CheckTreeNode[]>([])

  // ──── 加载状态 ────
  loading = ref(false)
  saving = ref(false)

  // ──── 本地缓存 ────
  protected associationCache = reactive<Map<string, Set<string>>>(new Map())
  protected cacheLoaded = false

  // ──── API 注入 ────
  protected api: RoleTreeApi

  /** 表格列配置（子类定义） */
  abstract columns: Array<{ prop: string; label: string; minWidth?: number; width?: number }>

  /** 构造函数：注入 API */
  constructor(api: RoleTreeApi) {
    this.api = api
  }

  // ========================================================
  // 初始化：加载本地缓存
  // ========================================================

  async initCache(): Promise<void> {
    if (this.cacheLoaded) return
    try {
      const associations = await this.api.getAllAssociations()
      this.buildCache(associations)
      this.cacheLoaded = true
    } catch (e: any) {
      console.error(`[${this.constructor.name}] 加载关联缓存失败:`, e)
    }
  }

  protected buildCache(associations: AssociationDto[]): void {
    this.associationCache.clear()
    for (const item of associations) {
      if (!this.associationCache.has(item.ContextCode)) {
        this.associationCache.set(item.ContextCode, new Set())
      }
      this.associationCache.get(item.ContextCode)!.add(item.TargetCode)
    }
  }

  // ========================================================
  // 角色树加载
  // ========================================================

  async loadRoleTreeRoot(): Promise<RoleTreeItem[]> {
    try {
      const items = await this.api.getRoleTreeRoot()
      this.roleTreeData.value = items
      return items
    } catch (e: any) {
      ElMessage.error(e.message || '加载角色树失败')
      return []
    }
  }

  async loadRoleTreeChildren(
    node: any,
    resolve: (data: RoleTreeItem[]) => void,
  ): Promise<void> {
    try {
      const items = await this.api.getRoleTreeChildren(node.data.Code, node.level ?? 0)
      resolve(items)
    } catch (e: any) {
      ElMessage.error(e.message || '加载子节点失败')
      resolve([])
    }
  }

  // ========================================================
  // Badge
  // ========================================================

  getCountForRole(roleCode: string): number {
    return this.associationCache.get(roleCode)?.size ?? 0
  }

  getRoleBadge(roleCode: string): string | undefined {
    const count = this.getCountForRole(roleCode)
    return count > 0 ? String(count) : undefined
  }

  // ========================================================
  // 角色选择
  // ========================================================

  async handleRoleSelect(role: RoleTreeItem): Promise<void> {
    if (!role || !role.Code) return

    this.selectedRole.value = role
    this.loading.value = true

    try {
      const data = await this.api.getCheckTree(role.Code)

      // 将 Extra 平铺到节点
      for (const node of data) {
        if (node.Extra) Object.assign(node, node.Extra)
      }

      // 用本地缓存覆盖 CheckFlag
      const cached = this.associationCache.get(role.Code) ?? new Set<string>()
      for (const node of data) {
        node.CheckFlag = cached.has(node.Code)
      }

      // 子类后处理（如分组跟随逻辑）
      this.handleRoleSelectPostProcess(data, cached)

      this.checkTreeData.value = data
    } catch (e: any) {
      ElMessage.error(e.message || '加载数据失败')
      this.checkTreeData.value = []
    } finally {
      this.loading.value = false
    }
  }

  /** 子类覆写：角色选择后处理（如 role-api 的分组跟随） */
  protected handleRoleSelectPostProcess(
    _data: CheckTreeNode[],
    _cached: Set<string>,
  ): void {
    // 默认空实现
  }

  // ========================================================
  // 勾选/取消
  // ========================================================

  async handleCheckChange(payload: { added: string[]; removed: string[] }): Promise<void> {
    if (!this.selectedRole.value) {
      ElMessage.warning('请先选择左侧角色')
      return
    }

    const roleCode = this.selectedRole.value.Code
    this.saving.value = true

    try {
      if (payload.added.length > 0) {
        const selections = this.buildSelections(payload.added)
        if (selections.length > 0) {
          const res = await this.api.checkAdd(roleCode, selections)
          // 用服务端回传的 Applied（实际生效集合，含其自动补全的祖先）局部更新缓存。
          // 以前这里会再调一次 check/all 把全部角色的关联全量拉回来 ——
          // 每勾一个复选框就全表重取一次，既慢又没必要。
          this.syncCacheAdd(roleCode, res.Applied ?? selections.map((s) => s.Code))
        }
      }

      if (payload.removed.length > 0) {
        const selections = this.buildSelections(payload.removed)
        if (selections.length > 0) {
          await this.api.checkRemove(roleCode, selections)
          // check/remove 删除的就是发过去的那批 code，无需服务端回传
          this.syncCacheRemove(roleCode, selections.map((s) => s.Code))
        }
      }

      ElMessage.success('保存成功')
    } catch (e: any) {
      ElMessage.error(e.message || '保存失败')
    } finally {
      this.saving.value = false
    }
  }

  /** 向缓存中追加某个角色已授权的 code（局部更新，不重取全表） */
  protected syncCacheAdd(roleCode: string, codes: string[]): void {
    let cache = this.associationCache.get(roleCode)
    if (!cache) {
      cache = new Set<string>()
      this.associationCache.set(roleCode, cache)
    }
    for (const code of codes) cache.add(code)
  }

  /** 从缓存中移除某个角色已取消授权的 code（局部更新） */
  protected syncCacheRemove(roleCode: string, codes: string[]): void {
    const cache = this.associationCache.get(roleCode)
    if (!cache) return
    for (const code of codes) cache.delete(code)
  }

  // ========================================================
  // 辅助方法（子类定义过滤的 NodeType）
  // ========================================================

  /** 子类定义：过滤哪些 NodeType 可被勾选 */
  protected abstract readonly selectableNodeTypes: string[]

  protected buildSelections(codes: string[]): TreeNodeSelection[] {
    const selections: TreeNodeSelection[] = []
    const allNodes = this.checkTreeData.value

    for (const code of codes) {
      const node = allNodes.find((n) => n.Code === code)
      if (node && this.selectableNodeTypes.includes(node.NodeType)) {
        selections.push({ Code: code, NodeType: node.NodeType })
      }
    }

    return selections
  }
}
