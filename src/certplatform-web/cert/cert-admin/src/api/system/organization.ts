/**
 * SystemOrg API - 组织机构管理
 *
 * 设计原则：
 * - 公共契约类型从 @share/types/contracts 导入
 * - 无业务 TS 实体，数据传输使用 Record<string, any>
 * - UI 由后端 JSON Config 驱动
 */

import { yzhApi, type ApiResponse, type TreeItemDto, type PagedData } from '@yzh-core'

// ========================================================
// 树接口 - 机构
// ========================================================

/** 加载根节点 */
export function getOrgRootNodes(): Promise<TreeItemDto[]> {
  return yzhApi.post('/api/System/Organization/tree/root', {})
}

/** 懒加载子节点 */
export function getOrgChildren(parentCode: string, level: number): Promise<TreeItemDto[]> {
  return yzhApi.post('/api/System/Organization/tree/children', { parentCode, level })
}

/** 新增机构 */
export function addOrg(data: Record<string, any>): Promise<any> {
  return yzhApi.post('/api/System/Organization/tree/add', data)
}

/** 修改机构 */
export function updateOrg(data: Record<string, any>): Promise<any> {
  return yzhApi.post('/api/System/Organization/tree/update', data)
}

/** 删除机构 */
export function deleteOrg(codes: string[]): Promise<any> {
  return yzhApi.post('/api/System/Organization/tree/delete', codes)
}

// ========================================================
// 人员接口（表格 CRUD）
// ========================================================

/** 分页查询人员 */
export function getUserPage(params: {
  page: number
  pageSize: number
  sortField?: string
  sortOrder?: string
  filters?: Array<{ field: string; operator: string; value: any }>
}): Promise<PagedData<any>> {
  return yzhApi.post('/api/System/Organization/filter', params)
}

/** 新增人员 */
export function addUser(data: Record<string, any>): Promise<any> {
  return yzhApi.post('/api/System/Organization/add', data)
}

/** 修改人员 */
export function updateUser(data: Record<string, any>): Promise<any> {
  return yzhApi.post('/api/System/Organization/update', data)
}

/** 删除人员 */
export function deleteUsers(codes: string[]): Promise<any> {
  return yzhApi.post('/api/System/Organization/delete', codes)
}

// ========================================================
// 配置接口
// ========================================================

/** 获取页面配置（TreeTableConfig） */
export function getOrgConfig(): Promise<any> {
  return yzhApi.get('/api/System/Organization/config')
}
