import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { ISOClause } from '../types/cert'

export async function getISOClausePage(params: PageParams): Promise<Page<ISOClause>> {
  return yzhApi.post<Page<ISOClause>>('/api/ISOClause/getPageData', params)
}

export async function addISOClause(data: Partial<ISOClause>): Promise<any> {
  return yzhApi.post('/api/ISOClause/add', data)
}

export async function updateISOClause(data: Partial<ISOClause>): Promise<any> {
  return yzhApi.post('/api/ISOClause/update', data)
}

export async function deleteISOClause(id: number): Promise<any> {
  return yzhApi.post(`/api/ISOClause/delete?id=${id}`)
}
