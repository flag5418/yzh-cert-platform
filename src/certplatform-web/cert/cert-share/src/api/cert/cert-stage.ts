import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { CertStage } from '../../types/cert'

export async function getCertStagePage(params: PageParams): Promise<Page<CertStage>> {
  return yzhApi.post<Page<CertStage>>('/api/CertStage/getPageData', params)
}

export async function addCertStage(data: Partial<CertStage>): Promise<any> {
  return yzhApi.post('/api/CertStage/add', data)
}

export async function updateCertStage(data: Partial<CertStage>): Promise<any> {
  return yzhApi.post('/api/CertStage/update', data)
}

export async function deleteCertStage(id: number): Promise<any> {
  return yzhApi.post(`/api/CertStage/delete?id=${id}`)
}
