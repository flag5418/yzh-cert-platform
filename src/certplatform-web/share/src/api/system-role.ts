import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface SysRole {
  role_Id: number
  roleName: string
  roleMark: string
  enable: number
  createDate: string
}

export async function getRolePage(params: PageParams): Promise<Page<SysRole>> {
  return yzhApi.post<Page<SysRole>>('/api/Sys_Role/getPageData', params)
}

export async function getRoleOptions(): Promise<Array<{ label: string; value: number }>> {
  return yzhApi.post<Array<{ label: string; value: number }>>('/api/Sys_Role/getList')
}
