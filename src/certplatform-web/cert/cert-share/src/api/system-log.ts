import http from '@yzh-core/utils/http'
import type { ApiResponse as HttpApiResponse } from '@yzh-core/utils/http'
import type { PageParams, Page } from '@yzh-core/components/table/types'

export interface SysLog {
  Id: number
  UserId?: number
  Module: string
  Action: string
  TargetType?: string
  TargetId?: number
  Detail?: string
  IpAddress?: string
  UserAgent?: string
  CreateTime?: string
}

export async function getLogPage(params: PageParams): Promise<Page<SysLog>> {
  const res = await http.post<HttpApiResponse<{ Items: SysLog[]; TotalCount: number }>>('/SysLog/filter', {
    Page: params.page,
    PageSize: params.pageSize,
    ...(params.filters || {})
  })
  return { rows: res.data?.Items ?? [], total: res.data?.TotalCount ?? 0 }
}
