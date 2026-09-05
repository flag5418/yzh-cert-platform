/**
 * 系统用户 API - 体系认证平台自研 API 层
 * 完全脱离 vol 的 api 自动生成机制，使用纯手写 API 封装
 */
import type { Page, PageParams } from '../components/table/types'
import { yzhApi } from './client'

export interface SysUser {
  user_Id: number
  userName: string
  userTrueName: string
  role_Id: number | null
  roleName?: string
  dept_Id?: string | null
  deptName?: string | null
  orgId?: string | null
  email?: string
  phoneNo?: string
  address?: string
  gender?: number
  enable: number
  createDate: string
  lastLoginDate?: string | null
  remark?: string
}

export interface UserSearchParams extends PageParams {
  userName?: string
  userTrueName?: string
  role_Id?: number
  enable?: number
  createDateRange?: string[]
}

/**
 * 分页查询用户
 */
export async function getUserPage(params: UserSearchParams): Promise<Page<SysUser>> {
  return yzhApi.post<Page<SysUser>>('/api/Sys_User/getPageData', params)
}

/**
 * 获取单个用户
 */
export async function getUser(id: number): Promise<SysUser> {
  return yzhApi.get<SysUser>('/api/Sys_User/getDetail', { id })
}

/**
 * 新增/编辑用户
 */
export async function saveUser(user: Partial<SysUser> & { user_Id?: number }): Promise<void> {
  if (user.user_Id) {
    return yzhApi.put<void>('/api/Sys_User/update', user)
  }
  return yzhApi.post<void>('/api/Sys_User/add', user)
}

/**
 * 删除用户
 */
export async function deleteUser(id: number | number[]): Promise<void> {
  const ids = Array.isArray(id) ? id.join(',') : id
  return yzhApi.delete<void>('/api/Sys_User/delete', { params: { ids } })
}

/**
 * 批量启用/禁用
 */
export async function toggleUserEnable(ids: number[], enable: boolean): Promise<void> {
  return yzhApi.post<void>('/api/Sys_User/enable', { ids, enable })
}
