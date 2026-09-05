/**
 * 认证阶段 API - V4 自研
 */
import { yzhApi } from './client'
import type { Page, PageParams } from '../components/table/types'

export interface CertStage {
  Id: number
  StageCode: string
  StageName: string
  OrderNo?: number
  StageType?: string
  StageTypeName?: string
  Description?: string
  Enable?: number
  CreateDate?: string
  ModifyDate?: string
}

export interface CertStageSearchParams extends PageParams {
  keyword?: string
  StageType?: string
}

export async function getCertStagePage(params: CertStageSearchParams): Promise<Page<CertStage>> {
  return yzhApi.post<Page<CertStage>>('/api/CertStage/getPageData', params)
}

export async function saveCertStage(row: Partial<CertStage> & { Id?: number }): Promise<void> {
  if (row.Id) {
    return yzhApi.put<void>('/api/CertStage/update', row)
  }
  return yzhApi.post<void>('/api/CertStage/add', row)
}

export async function deleteCertStage(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/CertStage/delete', { params: { ids: v } })
}
