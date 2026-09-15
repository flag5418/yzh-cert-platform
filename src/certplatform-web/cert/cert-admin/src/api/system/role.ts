import { yzhApi, type ApiResponse } from '@yzh-core/api/client'
import type { PageParams } from '@yzh-core/types'
import type { PagedData } from '@yzh-core/types/contracts'

export interface SysRole {
  Code: string
  RoleName: string
  ParentCode?: string
  ParentId: number
  Enable: number
  OrderNo?: number
  CreateDate?: string
}

export async function getRolePage(params: PageParams): Promise<PagedData<SysRole>> {
  const res = await yzhApi.post<ApiResponse<PagedData<SysRole>>>('/api/System/Role/filter', params)
  return res.data ?? { Items: [], TotalCount: 0 }
}

export async function getRoleOptions(): Promise<Array<{ label: string; value: string }>> {
  const res = await yzhApi.post<ApiResponse<PagedData<SysRole>>>('/api/System/Role/filter', {
    Page: 1,
    PageSize: 1000,
  })
  return (res.data?.Items ?? []).map((r) => ({
    label: r.RoleName,
    value: r.Code,
  }))
}
