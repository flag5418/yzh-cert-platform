import type { Page, PageParams } from '@yzh-core'
import { getEntityPage } from './generic'

/**
 * @file 系统日志 API
 * @note 走泛型 CRUD（generic.ts），控制器名 System/Log
 *       后端：SysLogController（只读 —— GetToolbar 已关闭 Add/Delete，GetRowButtons 已关闭 Edit/Delete）
 *
 * 修复记录（2026-09-22）：
 *   原实现存在四处契约偏差，任一都会导致 404 或空列表：
 *   1. 路径缺 `/api` 前缀（`/SysLog/filter` → `/api/System/Log/filter`）
 *   2. 控制器名错（`SysLog` → `System/Log`，后者才是实际类级路由）
 *   3. 直接读信封字段（`res.Items`）而非 `res.data.Items`
 *   4. 入参用了不存在的 `params.pageSize`（PageParams 的分页字段是 `rows`）
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

/**
 * 系统日志分页查询
 * @param params YzhTable 数据加载器入参：{ page, rows, sort, order, ...搜索条件 }
 *               搜索条件的 key = EntityConfig.SearchFields[].Field（PascalCase）
 */
export async function getLogPage(params: PageParams): Promise<Page<SysLog>> {
  const { page = 1, rows = 20, sort, order, ...searchValues } = params

  const res = await getEntityPage<SysLog>('System/Log', {
    Page: page,
    PageSize: rows,
    SortField: sort,
    SortOrder: order as 'asc' | 'desc' | undefined,
    Filters: Object.entries(searchValues)
      .filter(([, v]) => v !== undefined && v !== null && v !== '')
      .map(([Field, Value]) => ({ Field, Operator: 'eq' as const, Value })),
  })

  return { rows: res?.Items ?? [], total: res?.TotalCount ?? 0 }
}
