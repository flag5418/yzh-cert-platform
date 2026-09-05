import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { ISOStandard } from '../types/cert'

/** 获取 ISO 标准分页列表 */
export async function getISOStandardPage(params: PageParams): Promise<Page<ISOStandard>> {
  return yzhApi.post<Page<ISOStandard>>('/api/ISOStandard/getPageData', params)
}

/** 新增 ISO 标准 */
export async function addISOStandard(data: Partial<ISOStandard>): Promise<any> {
  return yzhApi.post('/api/ISOStandard/add', data)
}

/** 修改 ISO 标准 */
export async function updateISOStandard(data: Partial<ISOStandard>): Promise<any> {
  return yzhApi.post('/api/ISOStandard/update', data)
}

/** 删除 ISO 标准 */
export async function deleteISOStandard(id: number): Promise<any> {
  return yzhApi.post(`/api/ISOStandard/delete?id=${id}`)
}

/** 获取 ISO 标准列表（下拉用） */
export async function getISOStandardList(): Promise<ISOStandard[]> {
  return yzhApi.post<ISOStandard[]>('/api/ISOStandard/getList')
}
