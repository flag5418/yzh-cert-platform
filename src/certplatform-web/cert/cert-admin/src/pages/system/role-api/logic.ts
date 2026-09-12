/**
 * RoleApiLogic - 角色-接口权限管理 Logic
 *
 * 功能：
 * - 左侧：角色树（懒加载）+ 已授权接口数量 badge
 * - 右侧：接口分组树 + 接口勾选（el-table tree 模式，带 checkbox）
 * - 勾选/取消立即保存（auto-save）
 *
 * 本地缓存设计：
 * - 页面加载时调用 getAllAssociations() 获取全部角色-接口关联（1 次 API）
 * - 切换角色时从本地缓存计算 CheckFlag（0 次 API）
 * - 勾选/取消时同步更新本地缓存 + 调用 API
 * - 左侧角色树 badge 从本地缓存实时计算
 */

import { ref, reactive } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getRoleTreeRoot,
  getRoleTreeChildren,
  getCheckTree,
  checkAdd,
  checkRemove,
  getAllAssociations,
  type CheckTreeNode,
  type TreeNodeSelection,
  type AssociationDto,
  type RoleTreeItem,
} from '@/api/system/role-api'

// ========================================================
// Logic
// ========================================================

export class RoleApiLogic {
  // ──── 左侧角色树 ────
  roleTreeData = ref<RoleTreeItem[]>([])
  selectedRole = ref<RoleTreeItem | null>(null)

  // ──── 右侧接口树数据 ────
  checkTreeData = ref<CheckTreeNode[]>([])

  // ──── 加载状态 ────
  loading = ref(false)
  saving = ref(false)

  // ──── 本地缓存：Map<roleCode, Set<apiCode>> ────
  private associationCache = reactive<Map<string, Set<string>>>(new Map())
  private cacheLoaded = false

  // ──── 表格列配置 ────
  columns = [
    { prop: 'Name', label: '接口名称', minWidth: 250 },
    { prop: 'Method', label: '方法', width: 80 },
    { prop: 'Path', label: '路径', minWidth: 220 },
    { prop: 'ApiName', label: '描述', minWidth: 180 },
  ]

  // ========================================================
  // 初始化：加载本地缓存
  // ========================================================

  async initCache(): Promise<void> {
    if (this.cacheLoaded) return

    try {
      const associations = await getAllAssociations()
      this.buildCache(associations)
      this.cacheLoaded = true
    } catch (e: any) {
      console.error('[RoleApiLogic] 加载关联缓存失败:', e)
    }
  }

  private buildCache(associations: AssociationDto[]): void {
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
      const items = await getRoleTreeRoot()
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
      const items = await getRoleTreeChildren(node.data.Code, node.level ?? 0)
      resolve(items)
    } catch (e: any) {
      ElMessage.error(e.message || '加载子节点失败')
      resolve([])
    }
  }

  // ========================================================
  // Badge：角色的已授权接口数量
  // ========================================================

  getApiCount(roleCode: string): number {
    return this.associationCache.get(roleCode)?.size ?? 0
  }

  getRoleBadge(roleCode: string): string | undefined {
    const count = this.getApiCount(roleCode)
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
      const data = await getCheckTree(role.Code)

      for (const node of data) {
        if (node.Extra) Object.assign(node, node.Extra)
      }

      const cached = this.associationCache.get(role.Code) ?? new Set<string>()
      for (const node of data) {
        node.CheckFlag = cached.has(node.Code)
      }

      this.checkTreeData.value = data
    } catch (e: any) {
      ElMessage.error(e.message || '加载接口数据失败')
      this.checkTreeData.value = []
    } finally {
      this.loading.value = false
    }
  }

  // ========================================================
  // 勾选/取消（auto-save + 同步本地缓存）
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
          await checkAdd(roleCode, selections)

          if (!this.associationCache.has(roleCode)) {
            this.associationCache.set(roleCode, new Set())
          }
          const cache = this.associationCache.get(roleCode)!
          for (const sel of selections) cache.add(sel.Code)

          await this.syncCacheFromServer()
        }
      }

      if (payload.removed.length > 0) {
        const selections = this.buildSelections(payload.removed)
        if (selections.length > 0) {
          await checkRemove(roleCode, selections)

          const cache = this.associationCache.get(roleCode)
          if (cache) {
            for (const sel of selections) cache.delete(sel.Code)
          }
        }
      }

      ElMessage.success('保存成功')
    } catch (e: any) {
      ElMessage.error(e.message || '保存失败')
    } finally {
      this.saving.value = false
    }
  }

  private async syncCacheFromServer(): Promise<void> {
    try {
      const associations = await getAllAssociations()
      this.buildCache(associations)
    } catch (e: any) {
      console.error('[RoleApiLogic] 同步关联缓存失败:', e)
    }
  }

  // ========================================================
  // 辅助方法
  // ========================================================

  private buildSelections(codes: string[]): TreeNodeSelection[] {
    const selections: TreeNodeSelection[] = []
    const allNodes = this.checkTreeData.value

    for (const code of codes) {
      const node = allNodes.find((n) => n.Code === code)
      if (node && node.NodeType === 'api') {
        selections.push({ Code: code, NodeType: node.NodeType })
      }
    }

    return selections
  }
}
