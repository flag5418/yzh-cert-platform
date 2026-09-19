import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/api/client'
import type { CheckTreeNode, TreeNodeSelection, AssociationDto, RoleTreeItem } from '@share/types'
import { unwrap } from '@yzh-core/utils'

// ========================================================
// API 方法
// ========================================================

/** 左侧角色树 - 根节点 */
export async function getRoleTreeRoot(): Promise<RoleTreeItem[]> {
  const res = await yzhApi.post<ApiResponse<RoleTreeItem[]>>('/api/RoleApi/tree/root', {})
  return unwrap(res, [])
}

/** 左侧角色树 - 子节点（懒加载） */
export async function getRoleTreeChildren(parentCode: string, level = 0): Promise<RoleTreeItem[]> {
  const res = await yzhApi.post<ApiResponse<RoleTreeItem[]>>('/api/RoleApi/tree/children', {
    ParentCode: parentCode,
    Level: level,
  })
  return unwrap(res, [])
}

/** 右侧接口树数据（含勾选状态） */
export async function getCheckTree(roleCode: string): Promise<CheckTreeNode[]> {
  const res = await yzhApi.post<ApiResponse<CheckTreeNode[]>>('/api/RoleApi/checkTree', {
    ContextCode: roleCode,
  })
  return unwrap(res, [])
}

/**
 * 勾选保存（给角色授权接口）
 * `Applied` = 服务端确认「现已授权」的 code 集合（供前端局部更新关联缓存）
 */
export async function checkAdd(
  roleCode: string,
  selections: TreeNodeSelection[],
): Promise<{ Updated: number; Applied?: string[] }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number; Applied?: string[] }>>(
    '/api/RoleApi/check/add',
    {
      ContextCode: roleCode,
      Selections: selections,
    },
  )
  return unwrap(res, { Updated: 0 })
}

/** 取消勾选（移除角色与接口的关联） */
export async function checkRemove(
  roleCode: string,
  selections: TreeNodeSelection[],
): Promise<{ Updated: number }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number }>>('/api/RoleApi/check/remove', {
    ContextCode: roleCode,
    Selections: selections,
  })
  return unwrap(res, { Updated: 0 })
}

/** 获取所有角色-接口关联（前端本地缓存） */
export async function getAllAssociations(): Promise<AssociationDto[]> {
  const res = await yzhApi.post<ApiResponse<AssociationDto[]>>('/api/RoleApi/check/all', {})
  return unwrap(res, [])
}
