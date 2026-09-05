/**
 * 企业 API - V4 自研
 */
import { yzhApi } from './client'
import type { Page, PageParams } from '../components/table/types'

export interface Enterprise {
  Id: number
  EntCode: string
  EntName: string
  EntShortName?: string
  Industry?: string
  IndustryName?: string
  Scale?: string
  ScaleName?: string
  Country?: string
  Province?: string
  City?: string
  Address?: string
  ContactName?: string
  ContactPhone?: string
  ContactEmail?: string
  LegalPerson?: string
  EstablishDate?: string
  Enable: number
  Remark?: string
  CreateDate?: string
  ModifyDate?: string
}

export interface EnterpriseSearchParams extends PageParams {
  keyword?: string
  Industry?: string
  Scale?: string
  Enable?: number
}

export async function getEnterprisePage(params: EnterpriseSearchParams): Promise<Page<Enterprise>> {
  return yzhApi.post<Page<Enterprise>>('/api/Enterprise/getPageData', params)
}

export async function saveEnterprise(row: Partial<Enterprise> & { Id?: number }): Promise<void> {
  if (row.Id) {
    return yzhApi.put<void>('/api/Enterprise/update', row)
  }
  return yzhApi.post<void>('/api/Enterprise/add', row)
}

export async function deleteEnterprise(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/Enterprise/delete', { params: { ids: v } })
}
