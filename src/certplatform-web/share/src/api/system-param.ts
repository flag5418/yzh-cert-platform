import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface SysParam {
  id: number
  paramCode: string
  paramName: string
  paramValue: string
  paramType: string
  description?: string
  enable: number
  createDate?: string
}

export async function getParamList(): Promise<SysParam[]> {
  return yzhApi.post<SysParam[]>('/api/Sys_Parameter/getList')
}

export async function getParamPage(params: PageParams): Promise<Page<SysParam>> {
  return yzhApi.post<Page<SysParam>>('/api/Sys_Parameter/getPageData', params)
}

export async function saveParam(data: Partial<SysParam>): Promise<any> {
  return yzhApi.post('/api/Sys_Parameter/save', data)
}

export async function deleteParam(id: number): Promise<any> {
  return yzhApi.post(`/api/Sys_Parameter/delete?id=${id}`)
}

export async function getParamValue(code: string): Promise<string> {
  return yzhApi.get<string>(`/api/Sys_Parameter/getValue?paramCode=${code}`)
}
