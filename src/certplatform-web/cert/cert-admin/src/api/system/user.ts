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

/**
 * 获取用户分页数据
 * @deprecated 后端已提供 /api/System/User/filter，此方法应迁移到新路径
 */
export async function getUserPage(params: PageParams): Promise<Page<SysUser>> {
  return yzhApi.post<Page<SysUser>>('/api/System/User/filter', params)
}

export async function saveUser(user: Partial<SysUser>): Promise<void> {
  if (user.User_Id) {
    return yzhApi.put<void>('/api/System/User/update', user)
  }
  return yzhApi.post<void>('/api/System/User/add', user)
}

export async function deleteUser(ids: number | number[]): Promise<void> {
  const idList = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/System/User/delete', { params: { ids: idList } })
}
