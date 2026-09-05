import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface SysLog {
  log_Id: number
  module: string
  action: string
  userId: number
  userName: string
  ip: string
  userAgent: string
  result: string
  msg: string
  createDate: string
}

export async function getLogPage(params: PageParams): Promise<Page<SysLog>> {
  return yzhApi.post<Page<SysLog>>('/api/Sys_Log/getPageData', params)
}
