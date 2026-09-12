import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { ISOStandard } from '../../types/cert'

/** 获取 ISO 标准分页列表 */
export async function getISOStandardPage(params: PageParams): Promise<Page<ISOStandard>> {
  return yzhApi.post<Page<ISOStandard>>('/api/Foundation/ISOStandard/filter', params)
}

/** 新增 ISO 标准 */
export async function addISOStandard(data: Partial<ISOStandard>): Promise<any> {
  return yzhApi.post('/api/Foundation/ISOStandard/add', data)
}

/** 修改 ISO 标准 */
export async function updateISOStandard(data: Partial<ISOStandard>): Promise<any> {
  return yzhApi.post('/api/Foundation/ISOStandard/update', data)
}

/** 删除 ISO 标准（传 Code 数组） */
export async function deleteISOStandard(codes: string[]): Promise<any> {
  return yzhApi.post('/api/Foundation/ISOStandard/delete', codes)
}

/** 获取 ISO 标准下拉列表（供条款页面选择） */
export async function getISOStandardList(): Promise<{ rows: ISOStandard[] }> {
  return yzhApi.post<{ rows: ISOStandard[] }>('/api/Foundation/ISOStandard/filter', {
    page: 1,
    pageSize: 1000
  })
}
