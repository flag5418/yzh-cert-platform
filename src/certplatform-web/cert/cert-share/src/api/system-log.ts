import { yzhApi } from '@yzh-core/api/client'
import type { PageParams, Page } from '@yzh-core/components/table/types'

/**
 * @file 系统日志 API
 * @note 使用 yzhApi 替代 http.ts
 */

export interface SysLog {
  Id: number
  UserCode?: string
  Module: string
  Action: string
  TargetType?: string
  TargetCode?: string
  Detail?: string
  IpAddress?: string
  UserAgent?: string
  CreateTime?: string
}

export async function getLogPage(params: PageParams): Promise<Page<SysLog>> {
  const res = await yzhApi.post<{ Items: SysLog[]; TotalCount: number }>('/SysLog/filter', {
    Page: params.page,
    PageSize: params.pageSize,
    ...(params.filters || {})
  })
  return { rows: res?.Items ?? [], total: res?.TotalCount ?? 0 }
}
