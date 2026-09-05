/**
 * 系统日志 API - 体系认证平台自研 API 层
 */
import { yzhApi } from './client'
import type { Page, PageParams } from '../components/table/types'

export interface SysLog {
  log_Id: number
  logType?: string
  moduleName?: string
  actionName?: string
  requestUrl?: string
  requestMethod?: string
  requestParam?: string
  responseResult?: string
  exception?: string
  ip?: string
  browser?: string
  os?: string
  user_Id?: number
  userName?: string
  createDate?: string
  elapsedTime?: number
  status?: number
}

export interface LogSearchParams extends PageParams {
  logType?: string
  moduleName?: string
  userName?: string
  createDateRange?: string[]
}

export async function getLogPage(params: LogSearchParams): Promise<Page<SysLog>> {
  return yzhApi.post<Page<SysLog>>('/api/Sys_Log/getPageData', params)
}

export async function deleteLog(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/Sys_Log/delete', { params: { ids: v } })
}
