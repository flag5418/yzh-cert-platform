import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/api/client'

// ========================================================
// 类型定义
// ========================================================

/** 接口表实体 */
export interface ApiItem {
  Id: number
  Code: string
  Method: string
  Path: string
  GroupPath: string
  Name: string
  Author?: string
  Enable: boolean
  CreateDate: string
  UpdateDate?: string
}

/** 同步结果 */
export interface SyncResult {
  Added: number
  Updated: number
  Deleted: number
  Total: number
}

// ========================================================
// 工具：统一解包 ApiResponse
// ========================================================

function unwrap<T>(res: ApiResponse<T> | undefined, fallback: T): T {
  if (res && res.success === false) {
    throw new Error(res.message || '请求失败')
  }
  return (res?.data ?? fallback) as T
}

// ========================================================
// API 方法
// ========================================================

/** 获取所有接口列表 */
export async function getApiList(): Promise<ApiItem[]> {
  const res = await yzhApi.get<ApiResponse<ApiItem[]>>('/api/ApiSync/list')
  return unwrap(res, [])
}

/** 触发接口扫描并同步到数据库 */
export async function syncApis(): Promise<SyncResult> {
  const res = await yzhApi.post<ApiResponse<SyncResult>>('/api/ApiSync/sync', {})
  return unwrap(res, { Added: 0, Updated: 0, Deleted: 0, Total: 0 })
}

/** 扫描接口（不保存） */
export async function scanApis(): Promise<{ Total: number; Apis: any[] }> {
  const res = await yzhApi.get<ApiResponse<{ Total: number; Apis: any[] }>>('/api/ApiSync/scan')
  return unwrap(res, { Total: 0, Apis: [] })
}
