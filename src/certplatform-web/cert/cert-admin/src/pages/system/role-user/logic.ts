/**
 * RoleUserLogic - 角色-用户管理 Logic
 *
 * 继承 BaseRoleTreeLogic，覆写差异化部分
 * 特殊：角色树使用 yzhApi 直接调用（后端无独立 API 函数）
 */

import { yzhApi, type ApiResponse } from '@yzh-core/api/client'
import {
  getCheckTree,
  checkAdd,
  checkRemove,
  getAllAssociations,
} from '@/api/system/role-user'
import { BaseRoleTreeLogic, type RoleTreeItem } from '../_shared/BaseRoleTreeLogic'

/** 角色树节点（后端直接返回） */
interface RoleTreeNode {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType: string
  IsLeaf: boolean
  Extra?: Record<string, any>
  children: RoleTreeNode[]
}

/** 转换为 RoleTreeItem */
const toRoleTreeItem = (n: RoleTreeNode): RoleTreeItem => ({
  Code: n.Code,
  Name: n.Name,
  ParentCode: n.ParentCode ?? undefined,
  NodeType: n.NodeType,
  IsLeaf: n.IsLeaf,
  Extra: n.Extra,
})

export class RoleUserLogic extends BaseRoleTreeLogic {
  columns = [
    { prop: 'Name', label: '名称', minWidth: 200 },
    { prop: 'NodeType', label: '类型', width: 80 },
  ]

  protected readonly selectableNodeTypes = ['user']

  constructor() {
    super({
      getRoleTreeRoot: async () => {
        const res = await yzhApi.post<ApiResponse<RoleTreeNode[]>>('/api/Role/tree/root', {})
        return (res.data ?? []).map(toRoleTreeItem)
      },
      getRoleTreeChildren: async (parentCode: string) => {
        const res = await yzhApi.post<ApiResponse<RoleTreeNode[]>>('/api/Role/tree/children', {
          ParentCode: parentCode,
        })
        return (res.data ?? []).map(toRoleTreeItem)
      },
      getCheckTree,
      checkAdd,
      checkRemove,
      getAllAssociations,
    })
  }

  /** 获取角色的用户数量 */
  getUserCount(roleCode: string): number {
    return this.getCountForRole(roleCode)
  }

  /** 给角色树数据注入 badge（页面调用） */
  injectBadges(nodes: RoleTreeItem[]): void {
    for (const node of nodes) {
      const count = this.getCountForRole(node.Code)
      node.Extra = {
        ...node.Extra,
        badge: count > 0 ? String(count) : undefined,
      }
    }
  }
}
