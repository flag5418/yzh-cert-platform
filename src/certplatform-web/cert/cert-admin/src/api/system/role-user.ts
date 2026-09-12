import { yzhApi } from '@yzh-core/api/client'

// ========================================================
// 类型定义
// ========================================================

/** 混合树节点（后端 CheckTreeNodeDto） */
export interface CheckTreeNode {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType: string // 'org' | 'user' | 其他
  CheckFlag: boolean
  Extra?: Record<string, any>
}

/** 节点选择项 */
export interface TreeNodeSelection {
  Code: string
  NodeType: string
}

/** 关联关系 DTO（后端 AssociationDto） */
export interface AssociationDto {
  ContextCode: string  // 左侧上下文编码（如角色编码）
  TargetCode: string   // 右侧关联节点编码（如用户编码）
  NodeType: string     // 右侧节点类型
}

// ========================================================
// API 方法
// ========================================================

/**
 * 获取混合树数据（机构+用户）
 * @param roleCode 左侧选中的角色编码
 */
export async function getCheckTree(roleCode: string): Promise<CheckTreeNode[]> {
  const res = await yzhApi.post<CheckTreeNode[]>('/api/Role/checkTree', {
    ContextCode: roleCode,
  })
  return res.data ?? []
}

/**
 * 勾选保存（给角色分配用户）
 * @param roleCode 角色编码
 * @param selections 勾选的节点集合
 */
export async function checkAdd(
  roleCode: string,
  selections: TreeNodeSelection[],
): Promise<{ Updated: number }> {
  const res = await yzhApi.post<{ Updated: number }>('/api/Role/check/add', {
    ContextCode: roleCode,
    Selections: selections,
  })
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
  const res = await yzhApi.post<{ Updated: number }>('/api/Role/check/remove', {
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
  const res = await yzhApi.post<AssociationDto[]>('/api/Role/check/all', {})
  return res.data ?? []
}
