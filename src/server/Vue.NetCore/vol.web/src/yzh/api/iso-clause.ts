/**
 * ISO 条款 API - V4 自研
 */
import { yzhApi } from './client'
import type { Page, PageParams } from '../components/table/types'

export interface ISOClause {
  Id: number
  StandardId: number
  StandardCode?: string
  StandardName?: string
  ClauseCode: string
  ClauseTitle: string
  ClauseContent?: string
  OrderNo?: number
  Enable?: number
  CreateDate?: string
  ModifyDate?: string
}

export interface ISOClauseSearchParams extends PageParams {
  StandardId?: number
  keyword?: string
}

export async function getISOClausePage(params: ISOClauseSearchParams): Promise<Page<ISOClause>> {
  return yzhApi.post<Page<ISOClause>>('/api/ISOClause/getPageData', params)
}

export async function saveISOClause(row: Partial<ISOClause> & { Id?: number }): Promise<void> {
  if (row.Id) {
    return yzhApi.put<void>('/api/ISOClause/update', row)
  }
  return yzhApi.post<void>('/api/ISOClause/add', row)
}

export async function deleteISOClause(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/ISOClause/delete', { params: { ids: v } })
}
