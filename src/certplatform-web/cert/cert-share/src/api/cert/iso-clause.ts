import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { ISOClause } from '../../types/cert'

/** 获取 ISO 条款分页列表 */
export async function getISOClausePage(params: PageParams & { standardCode?: string }): Promise<Page<ISOClause>> {
  return yzhApi.post<Page<ISOClause>>('/api/Foundation/ISOClause/filter', params)
}

/** 新增 ISO 条款 */
export async function addISOClause(data: Partial<ISOClause>): Promise<any> {
  return yzhApi.post('/api/Foundation/ISOClause/add', data)
}

/** 修改 ISO 条款 */
export async function updateISOClause(data: Partial<ISOClause>): Promise<any> {
  return yzhApi.post('/api/Foundation/ISOClause/update', data)
}

/** 删除 ISO 条款（传 Code 数组） */
export async function deleteISOClause(codes: string[]): Promise<any> {
  return yzhApi.post('/api/Foundation/ISOClause/delete', codes)
}
