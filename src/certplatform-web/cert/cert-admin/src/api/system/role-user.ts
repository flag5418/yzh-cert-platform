import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/api/client'
import type { CheckTreeNode, TreeNodeSelection, AssociationDto } from '@share/types'

// ========================================================
// API 方法
// ========================================================

/**
 * 获取混合树数据（机构+用户）
 * @param roleCode 左侧选中的角色编码
 */
export async function getCheckTree(roleCode: string): Promise<CheckTreeNode[]> {
  const res = await yzhApi.post<ApiResponse<CheckTreeNode[]>>('/api/Role/checkTree', {
    ContextCode: roleCode,
  })
  return res.data ?? []
}

/**
 * 勾选保存（给角色分配用户）
 * @param roleCode 角色编码
 * @param selections 勾选的节点集合
 * `Applied` = 服务端确认「现已授权」的 code 集合（供前端局部更新关联缓存）
 */
export async function checkAdd(
  roleCode: string,
  selections: TreeNodeSelection[],
): Promise<{ Updated: number; Applied?: string[] }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number; Applied?: string[] }>>(
    '/api/Role/check/add',
    {
      ContextCode: roleCode,
      Selections: selections,
    },
  )
  return res.data ?? { Updated: 0 }
}

/**
 * 取消勾选（移除角色与用户的关联）
 * @param roleCode 角色编码
 * @param selections 取消勾选的节点集合
 */
export async function checkRemove(
  roleCode: string,
  selections: TreeNodeSelection[],
): Promise<{ Updated: number }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number }>>('/api/Role/check/remove', {
    ContextCode: roleCode,
    Selections: selections,
  })
  return res.data ?? { Updated: 0 }
}

/**
 * 获取所有角色-用户关联（用于前端本地缓存）
 * 页面加载时调用一次，后续切换角色时直接从本地缓存计算 CheckFlag
 */
export async function getAllAssociations(): Promise<AssociationDto[]> {
  const res = await yzhApi.post<ApiResponse<AssociationDto[]>>('/api/Role/check/all', {})
  return res.data ?? []
}
