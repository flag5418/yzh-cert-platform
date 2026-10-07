import { yzhApi } from '../client'
import type { ApiResponse } from '../client'
import type { AssociationSelection, CheckTreeNode, AssociationDto, RoleTreeItem } from '../../types/association'
import type { TreeTableConfigDto, TreeItemDto } from '../../types/contracts'
import { expectOk } from '../../utils/apiResponse'
import { unwrap } from '../../utils'

// ========================================================
// 角色树（role-user 无独立树端点，复用 /api/Role/tree/*）
// ========================================================

/** 角色树节点（后端直接返回） */
interface RoleTreeNode {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType: string
  IsLeaf: boolean
  Extra?: Record<string, any>
  children?: RoleTreeNode[]
}

/** 转换为 RoleTreeItem（children 不透传：懒加载由 el-tree 挂载） */
const toRoleTreeItem = (n: RoleTreeNode): RoleTreeItem => ({
  Code: n.Code,
  Name: n.Name,
  ParentCode: n.ParentCode ?? undefined,
  NodeType: n.NodeType,
  IsLeaf: n.IsLeaf,
  Extra: n.Extra,
})

/** 左侧角色树 - 根节点 */
export async function getRoleTreeRoot(): Promise<RoleTreeItem[]> {
  const res = await yzhApi.post<ApiResponse<RoleTreeNode[]>>('/api/Role/tree/root', {})
  return (res.data ?? []).map(toRoleTreeItem)
}

/** 左侧角色树 - 子节点（懒加载；载荷保持与原实现一致，仅 ParentCode） */
export async function getRoleTreeChildren(parentCode: string, _level?: number): Promise<RoleTreeItem[]> {
  const res = await yzhApi.post<ApiResponse<RoleTreeNode[]>>('/api/Role/tree/children', {
    ParentCode: parentCode,
  })
  return (res.data ?? []).map(toRoleTreeItem)
}

// ========================================================
// 角色树节点增删改（role-user 页面左侧角色树；与 role-menu / role-api 共用 Role 控制器）
// ========================================================

/**
 * 树行为配置 + 树节点表单配置（`GET /api/Role/treepconfig`，与 `TreeTableCore.loadConfig` 同方法）
 * 节点动作（新增下级/编辑/删除）与弹窗字段全部由它驱动，页面零硬编码。
 */
export async function getRoleTreePageConfig(): Promise<TreeTableConfigDto> {
  const res = await yzhApi.get<ApiResponse<TreeTableConfigDto>>('/api/Role/treepconfig')
  expectOk(res, '加载角色树配置失败')
  return res.data as TreeTableConfigDto
}

/**
 * 新增角色树节点（`/api/Role/tree/add`）
 * 请求体 PascalCase（RoleName / ParentCode / IsValid / OrderNo / Code…），
 * Code 由后端在钩子未生成时兜底 Guid。
 * F-1/F-2：失败抛 BizError，不返回给流程层。
 */
export async function addRoleTreeNode(body: Record<string, any>): Promise<TreeItemDto | null> {
  const res = await yzhApi.post<ApiResponse<TreeItemDto>>('/api/Role/tree/add', body)
  expectOk(res, '新增角色失败')
  return res.data ?? null
}

/** 修改角色树节点（`/api/Role/tree/update`，按 Code 定位，请求体不带 Id） */
export async function updateRoleTreeNode(body: Record<string, any>): Promise<TreeItemDto | null> {
  const res = await yzhApi.post<ApiResponse<TreeItemDto>>('/api/Role/tree/update', body)
  expectOk(res, '修改角色失败')
  return res.data ?? null
}

/** 删除角色树节点（`/api/Role/tree/delete`，body = Code[]；含子节点时后端拒绝） */
export async function deleteRoleTreeNodes(codes: string[]): Promise<void> {
  const res = await yzhApi.post<ApiResponse<string>>('/api/Role/tree/delete', codes)
  expectOk(res, '删除角色失败')
}

// ========================================================
// 关联（勾选授权）API
// ========================================================

/**
 * 获取混合树数据（机构+用户）
 * @param roleCode 左侧选中的角色编码
 */
export async function getCheckTree(roleCode: string): Promise<CheckTreeNode[]> {
  const res = await yzhApi.post<ApiResponse<CheckTreeNode[]>>('/api/Role/checkTree', {
    ContextCode: roleCode,
  })
  return unwrap(res, [])
}

/**
 * 勾选保存（给角色分配用户）
 * `Applied` = 服务端确认「现已授权」的 code 集合（供前端局部更新关联缓存）
 */
export async function checkAdd(
  roleCode: string,
  selections: AssociationSelection[],
): Promise<{ Updated: number; Applied?: string[] }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number; Applied?: string[] }>>(
    '/api/Role/check/add',
    {
      ContextCode: roleCode,
      Selections: selections,
    },
  )
  return unwrap(res, { Updated: 0 })
}

/** 取消勾选（移除角色与用户的关联） */
export async function checkRemove(
  roleCode: string,
  selections: AssociationSelection[],
): Promise<{ Updated: number }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number }>>('/api/Role/check/remove', {
    ContextCode: roleCode,
    Selections: selections,
  })
  return unwrap(res, { Updated: 0 })
}

/**
 * 获取所有角色-用户关联（用于前端本地缓存）
 * 页面加载时调用一次，后续切换角色时直接从本地缓存计算 CheckFlag
 */
export async function getAllAssociations(): Promise<AssociationDto[]> {
  const res = await yzhApi.post<ApiResponse<AssociationDto[]>>('/api/Role/check/all', {})
  return unwrap(res, [])
}
