import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface SysUser {
  User_Id: number
  UserName: string
  UserTrueName: string
  Role_Id: number | null
  RoleName?: string
  Enable: number
  PhoneNo?: string
  Email?: string
  CreateDate: string
  Remark?: string
}

export async function getUserPage(params: PageParams): Promise<Page<SysUser>> {
  return yzhApi.post<Page<SysUser>>('/api/Sys_User/getPageData', params)
}

export async function saveUser(user: Partial<SysUser>): Promise<void> {
  if (user.User_Id) {
    return yzhApi.put<void>('/api/Sys_User/update', user)
  }
  return yzhApi.post<void>('/api/Sys_User/add', user)
}

export async function deleteUser(ids: number | number[]): Promise<void> {
  const idList = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/Sys_User/delete', { params: { ids: idList } })
}
