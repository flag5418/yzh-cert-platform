/**
 * 认证机构 API - V4 自研
 */
import { yzhApi } from './client'
import type { Page, PageParams } from '../components/table/types'

export interface CertificationBody {
  Id: number
  CbCode: string
  CbName: string
  CbShortName?: string
  CbType?: string
  CbTypeName?: string
  Country?: string
  Province?: string
  City?: string
  Address?: string
  ContactName?: string
  ContactPhone?: string
  ContactEmail?: string
  Enable: number
  Remark?: string
  CreateDate?: string
  ModifyDate?: string
}

export interface CertBodySearchParams extends PageParams {
  keyword?: string
  CbType?: string
  Enable?: number
}

export async function getCertBodyPage(params: CertBodySearchParams): Promise<Page<CertificationBody>> {
  return yzhApi.post<Page<CertificationBody>>('/api/CertificationBody/getPageData', params)
}

export async function saveCertBody(row: Partial<CertificationBody> & { Id?: number }): Promise<void> {
  if (row.Id) {
    return yzhApi.put<void>('/api/CertificationBody/update', row)
  }
  return yzhApi.post<void>('/api/CertificationBody/add', row)
}

export async function deleteCertBody(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/CertificationBody/delete', { params: { ids: v } })
}
