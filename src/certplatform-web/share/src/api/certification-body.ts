import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { CertificationBody } from '../types/cert'

/** 获取认证机构分页列表 */
export async function getCertificationBodyPage(params: PageParams): Promise<Page<CertificationBody>> {
  return yzhApi.post<Page<CertificationBody>>('/api/CertificationBody/getPageData', params)
}

/** 新增认证机构 */
export async function addCertificationBody(data: Partial<CertificationBody>): Promise<any> {
  return yzhApi.post('/api/CertificationBody/add', data)
}

/** 修改认证机构 */
export async function updateCertificationBody(data: Partial<CertificationBody>): Promise<any> {
  return yzhApi.post('/api/CertificationBody/update', data)
}

/** 删除认证机构 */
export async function deleteCertificationBody(id: number): Promise<any> {
  return yzhApi.post(`/api/CertificationBody/delete?id=${id}`)
}

/** 获取认证机构列表（下拉用） */
export async function getCertificationBodyList(): Promise<CertificationBody[]> {
  return yzhApi.post<CertificationBody[]>('/api/CertificationBody/getList')
}
