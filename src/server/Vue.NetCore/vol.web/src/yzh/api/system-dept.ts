/**
 * 系统部门 API - 体系认证平台自研 API 层
 */
import { yzhApi } from './client'
import type { Page, PageParams } from '../components/table/types'

export interface SysDepartment {
  departmentId: string
  departmentName: string
  parentId?: string | null
  departmentCode?: string
  enable?: number
  remark?: string
  creator?: string
  createDate?: string
  modifyDate?: string
  children?: SysDepartment[]
}

export interface DeptSearchParams extends PageParams {
  departmentName?: string
  parentId?: number
  enable?: number
}

export async function getDeptPage(params: DeptSearchParams): Promise<Page<SysDepartment>> {
  return yzhApi.post<Page<SysDepartment>>('/api/Sys_Department/getPageData', params)
}

export async function getDeptTree(): Promise<SysDepartment[]> {
  return yzhApi.get<SysDepartment[]>('/api/Sys_Department/getTreeData')
}

export async function getDept(id: number): Promise<SysDepartment> {
  return yzhApi.get<SysDepartment>('/api/Sys_Department/getDetail', { id })
}

export async function saveDept(dept: Partial<SysDepartment> & { department_Id?: number }): Promise<void> {
  if (dept.department_Id) {
    return yzhApi.put<void>('/api/Sys_Department/update', dept)
  }
  return yzhApi.post<void>('/api/Sys_Department/add', dept)
}

export async function deleteDept(ids: string | string[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/Sys_Department/delete', { params: { ids: v } })
}
