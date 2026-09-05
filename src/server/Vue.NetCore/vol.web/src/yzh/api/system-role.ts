/**
 * 系统角色 API - 体系认证平台自研 API 层
 */
import type { Page, PageParams } from '../components/table/types'
import { yzhApi } from './client'

export interface SysRole {
  role_Id: number
  roleName: string
  roleCode?: string
  enable: number
  creator?: string
  createDate?: string
  modifyDate?: string
  remark?: string
}

export interface RoleSearchParams extends PageParams {
  roleName?: string
  roleCode?: string
  enable?: number
}

export async function getRolePage(params: RoleSearchParams): Promise<Page<SysRole>> {
  return yzhApi.post<Page<SysRole>>('/api/Sys_Role/getPageData', params)
}

export async function getRole(id: number): Promise<SysRole> {
  return yzhApi.get<SysRole>('/api/Sys_Role/getDetail', { id })
}

export async function saveRole(role: Partial<SysRole> & { role_Id?: number }): Promise<void> {
  if (role.role_Id) {
    return yzhApi.put<void>('/api/Sys_Role/update', role)
  }
  return yzhApi.post<void>('/api/Sys_Role/add', role)
}

export async function deleteRole(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/Sys_Role/delete', { params: { ids: v } })
}

export async function getRoleOptions(): Promise<Array<{ label: string; value: number }>> {
  const res = await yzhApi.post<Page<{ role_Id: number; roleName: string }>>(
    '/api/Sys_Role/getPageData',
    { page: 1, rows: 1000 }
  )
  return (res.rows || []).map((r) => ({ label: r.roleName, value: r.role_Id }))
}
