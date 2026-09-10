import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface SysRole {
  Code: string
  RoleName: string
  ParentCode?: string
  ParentId: number
  Enable: number
  OrderNo?: number
  CreateDate?: string
}

export async function getRolePage(params: PageParams): Promise<Page<SysRole>> {
  return yzhApi.post<Page<SysRole>>('/api/System/Role/filter', params)
}

export async function getRoleOptions(): Promise<Array<{ label: string; value: string }>> {
  const res = await yzhApi.post<Page<SysRole>>('/api/System/Role/filter', {
    Page: 1,
    PageSize: 1000,
  })
  return (res.data?.Items ?? []).map((r) => ({
    label: r.RoleName,
    value: r.Code,
  }))
}
