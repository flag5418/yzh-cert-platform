/**
 * RoleUserLogic - 角色-用户管理 Logic
 *
 * 功能：
 * - 左侧：角色树（懒加载）+ 用户数量 badge
 * - 右侧：机构+用户混合树形表格（el-table tree 模式，带 checkbox）
 * - 勾选/取消立即保存（auto-save）
 *
 * 本地缓存设计：
 * - 页面加载时调用 getAllAssociations() 获取全部角色-用户关联（1次 API）
 * - 切换角色时从本地缓存计算 CheckFlag（0次 API）
 * - 勾选/取消时同步更新本地缓存 + 调用 API
 * - 左侧角色树 badge 从本地缓存实时计算
 */

import { ref, reactive } from 'vue'
import { ElMessage } from 'element-plus'
import { yzhApi } from '@yzh-core/api/client'
import {
  getCheckTree,
  checkAdd,
  checkRemove,
  getAllAssociations,
  type CheckTreeNode,
  type TreeNodeSelection,
  type AssociationDto,
} from '@/api/system/role-user'

// ========================================================
// 类型定义
// ========================================================

/** 角色树节点（左侧） */
interface RoleTreeNode {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType: string
  IsLeaf: boolean
  Extra?: Record<string, any>
  children: RoleTreeNode[]
}

// ========================================================
// Logic
// ========================================================

export class RoleUserLogic {
  // ──── 左侧角色树 ────
  roleTreeData = ref<RoleTreeNode[]>([])
  selectedRole = ref<RoleTreeNode | null>(null)

  // ──── 右侧混合树数据 ────
  checkTreeData = ref<CheckTreeNode[]>([])

  // ──── 加载状态 ────
  loading = ref(false)
  saving = ref(false)

  // ──── 本地缓存：Map<roleCode, Set<userCode>> ────
  private associationCache = reactive<Map<string, Set<string>>>(new Map())

  // ──── 缓存是否已加载 ────
  private cacheLoaded = false

  // ──── 表格列配置 ────
  columns = [
    { prop: 'Name', label: '名称', minWidth: 200 },
    { prop: 'NodeType', label: '类型', width: 80 },
  ]

  // ========================================================
  // 初始化：加载本地缓存
  // ========================================================

  /** 页面加载时调用，获取所有关联关系到本地缓存 */
  async initCache(): Promise<void> {
    if (this.cacheLoaded) return

    try {
      const associations = await getAllAssociations()
      this.buildCache(associations)
      this.cacheLoaded = true
    } catch (e: any) {
      console.error('[RoleUserLogic] 加载关联缓存失败:', e)
    }
  }

  /** 从关联数组构建 Map<roleCode, Set<userCode>> */
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

  async loadRoleTreeRoot(): Promise<RoleTreeNode[]> {
    try {
      const res = await yzhApi.post<RoleTreeNode[]>('/api/Role/tree/root', {})
      const items = res.data ?? []
      this.roleTreeData.value = items as any
      return items as any
    } catch (e: any) {
      ElMessage.error(e.message || '加载角色树失败')
      return []
    }
  }

  async loadRoleTreeChildren(
    node: any,
    resolve: (data: RoleTreeNode[]) => void,
  ): Promise<void> {
    try {
      const res = await yzhApi.post<RoleTreeNode[]>('/api/Role/tree/children', {
        ParentCode: node.data.Code,
      })
      const items = res.data ?? []
      resolve(items as any)
    } catch (e: any) {
      ElMessage.error(e.message || '加载子节点失败')
      resolve([])
    }
  }

  // ========================================================
  // Badge 计算：从本地缓存获取角色的用户数量
  // ========================================================

  /** 获取角色的关联用户数量（从本地缓存） */
  getUserCount(roleCode: string): number {
    return this.associationCache.get(roleCode)?.size ?? 0
  }

  /** 获取角色的 badge 文本（有用户时返回数量，否则返回空） */
  getRoleBadge(roleCode: string): string | undefined {
    const count = this.getUserCount(roleCode)
    return count > 0 ? String(count) : undefined
  }

  /** 给角色树数据注入 badge（在 loadRoleTreeRoot 后调用） */
  injectBadges(nodes: RoleTreeNode[]): void {
    for (const node of nodes) {
      const count = this.getUserCount(node.Code)
      node.Extra = {
        ...node.Extra,
        badge: count > 0 ? String(count) : undefined,
      }
    }
  }

  // ========================================================
  // 角色选择 → 从本地缓存计算 CheckFlag（无需 API）
  // ========================================================

  async handleRoleSelect(role: RoleTreeNode): Promise<void> {
    if (!role || !role.Code) return

    this.selectedRole.value = role
    this.loading = true

    try {
      // 获取混合树数据（仍需要 API 获取 org+user 列表）
      const data = await getCheckTree(role.Code)

      // 从本地缓存覆盖 CheckFlag（不依赖后端返回的 CheckFlag）
      const cachedUserCodes = this.associationCache.get(role.Code) ?? new Set()
      for (const node of data) {
        if (node.NodeType === 'user') {
          node.CheckFlag = cachedUserCodes.has(node.Code)
        }
      }

      this.checkTreeData.value = data
    } catch (e: any) {
      ElMessage.error(e.message || '加载用户数据失败')
      this.checkTreeData.value = []
    } finally {
      this.loading = false
    }
  }

  // ========================================================
  // 勾选/取消处理（auto-save + 同步本地缓存）
  // ========================================================

  async handleCheckChange(payload: { added: string[]; removed: string[] }): Promise<void> {
    if (!this.selectedRole.value) {
      ElMessage.warning('请先选择左侧角色')
      return
    }

    const roleCode = this.selectedRole.value.Code
    this.saving = true

    try {
      // 处理新增勾选
      if (payload.added.length > 0) {
        const userSelections = this.buildUserSelections(payload.added)
        if (userSelections.length > 0) {
          await checkAdd(roleCode, userSelections)

          // 同步本地缓存
          if (!this.associationCache.has(roleCode)) {
            this.associationCache.set(roleCode, new Set())
          }
          const cache = this.associationCache.get(roleCode)!
          for (const sel of userSelections) {
            cache.add(sel.Code)
          }
        }
      }

      // 处理取消勾选
      if (payload.removed.length > 0) {
        const userSelections = this.buildUserSelections(payload.removed)
        if (userSelections.length > 0) {
          await checkRemove(roleCode, userSelections)

          // 同步本地缓存
          const cache = this.associationCache.get(roleCode)
          if (cache) {
            for (const sel of userSelections) {
              cache.delete(sel.Code)
            }
          }
        }
      }

      ElMessage.success('保存成功')
    } catch (e: any) {
      ElMessage.error(e.message || '保存失败')
    } finally {
      this.saving = false
    }
  }

  // ========================================================
  // 辅助方法
  // ========================================================

  /** 构建用户选择项（只取 NodeType 为 user 的节点） */
  private buildUserSelections(codes: string[]): TreeNodeSelection[] {
    const selections: TreeNodeSelection[] = []
    const allNodes = this.checkTreeData.value

    for (const code of codes) {
      const node = allNodes.find((n) => n.Code === code)
      if (node && node.NodeType === 'user') {
        selections.push({
          Code: code,
          NodeType: node.NodeType,
        })
      }
    }

    return selections
  }
}
