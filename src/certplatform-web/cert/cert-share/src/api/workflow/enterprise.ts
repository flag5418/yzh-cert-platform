import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { Enterprise } from '../../types/cert'

export async function getEnterprisePage(params: PageParams): Promise<Page<Enterprise>> {
  return yzhApi.post<Page<Enterprise>>('/api/Enterprise/getPageData', params)
}

export async function addEnterprise(data: Partial<Enterprise>): Promise<any> {
  return yzhApi.post('/api/Enterprise/add', data)
}

export async function updateEnterprise(data: Partial<Enterprise>): Promise<any> {
  return yzhApi.post('/api/Enterprise/update', data)
}

export async function deleteEnterprise(id: number): Promise<any> {
  return yzhApi.post(`/api/Enterprise/delete?id=${id}`)
}
