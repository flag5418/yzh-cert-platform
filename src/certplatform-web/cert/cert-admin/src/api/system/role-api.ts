import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/api/client'

// ========================================================
// 类型定义
// ========================================================

/** 接口勾选节点（后端 CheckTreeNodeDto） */
export interface CheckTreeNode {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType: string // 'group' | 'api'
  CheckFlag: boolean
  Extra?: Record<string, any>
}

/** 节点选择项（后端 TreeNodeSelection） */
export interface TreeNodeSelection {
  Code: string
  NodeType: string
}

/** 关联关系 DTO（后端 AssociationDto） */
export interface AssociationDto {
  ContextCode: string // 左侧上下文编码（角色编码）
  TargetCode: string // 右侧关联节点编码（接口编码）
  NodeType: string // 右侧节点类型
}

/** 树节点 DTO（后端 TreeItemDto，角色树） */
export interface RoleTreeItem {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType?: string
  IsLeaf?: boolean
  Level?: number
  Extra?: Record<string, any>
}

// ========================================================
// 工具：统一解包 ApiResponse
// ========================================================

function unwrap<T>(res: ApiResponse<T> | undefined, fallback: T): T {
  if (res && res.success === false) {
    throw new Error(res.message || '请求失败')
  }
  return (res?.data ?? fallback) as T
}

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

/** 勾选保存（给角色授权接口） */
export async function checkAdd(
  roleCode: string,
  selections: TreeNodeSelection[],
): Promise<{ Updated: number }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number }>>('/api/RoleApi/check/add', {
    ContextCode: roleCode,
    Selections: selections,
  })
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
