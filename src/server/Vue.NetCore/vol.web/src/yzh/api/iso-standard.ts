/**
 * ISO 标准注册 API - V4 自研
 *
 * 字段说明：后端视图已包含字典翻译（CategoryName/StatusName）
 */
import { yzhApi } from './client'
import type { Page, PageParams } from '../components/table/types'

export interface ISOStandard {
  Id: number
  StandardCode: string
  StandardName: string
  VersionYear: number
  Category: string
  CategoryName?: string
  Status: string
  StatusName?: string
  Description?: string
  Remark?: string
  CreateDate?: string
  ModifyDate?: string
}

export interface ISOStandardSearchParams extends PageParams {
  keyword?: string
  Category?: string
  Status?: string
}

export async function getISOStandardPage(params: ISOStandardSearchParams): Promise<Page<ISOStandard>> {
  return yzhApi.post<Page<ISOStandard>>('/api/ISOStandard/getPageData', params)
}

export async function saveISOStandard(row: Partial<ISOStandard> & { Id?: number }): Promise<void> {
  if (row.Id) {
    return yzhApi.put<void>('/api/ISOStandard/update', row)
  }
  return yzhApi.post<void>('/api/ISOStandard/add', row)
}

export async function deleteISOStandard(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/ISOStandard/delete', { params: { ids: v } })
}
