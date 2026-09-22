import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse, PagedData } from '@yzh-core'
import type { CertificationBody } from '../../types/cert'

/** 获取认证机构分页列表 */
export async function getCertificationBodyPage(params: any): Promise<ApiResponse<PagedData<CertificationBody>>> {
  return yzhApi.post<ApiResponse<PagedData<CertificationBody>>>('/api/Foundation/CertificationBody/filter', params)
}

/** 获取认证机构列表（不限分页，供下拉选择） */
export async function getCertificationBodyList(): Promise<ApiResponse<CertificationBody[]>> {
  return yzhApi.post<ApiResponse<CertificationBody[]>>('/api/Foundation/CertificationBody/filter', { page: 1, pageSize: 1000 })
}

/** 新增认证机构（后端会在同一事务内同步创建 Sys_Organization 机构记录） */
export async function addCertificationBody(data: Partial<CertificationBody>): Promise<ApiResponse<CertificationBody>> {
  return yzhApi.post<ApiResponse<CertificationBody>>('/api/Foundation/CertificationBody/add', data)
}

/** 修改认证机构（同步更新机构主体名称/编号） */
export async function updateCertificationBody(data: Partial<CertificationBody>): Promise<ApiResponse<CertificationBody>> {
  return yzhApi.post<ApiResponse<CertificationBody>>('/api/Foundation/CertificationBody/update', data)
}

/** 删除认证机构（同步软删除机构主体；有子机构时拒绝） */
export async function deleteCertificationBody(codes: string[]): Promise<ApiResponse<object>> {
  return yzhApi.post<ApiResponse<object>>('/api/Foundation/CertificationBody/delete', codes)
}

/** 启用/禁用认证机构（同步 Sys_Organization.Enable） */
export async function toggleCertificationBodyValid(code: string): Promise<ApiResponse<{ Code: string; IsValid: number }>> {
  return yzhApi.post<ApiResponse<{ Code: string; IsValid: number }>>('/api/Foundation/CertificationBody/toggle-valid', { Code: code })
}

/** 获取页面配置（EntityConfig） */
export async function getCertificationBodyConfig(): Promise<ApiResponse<any>> {
  return yzhApi.get<ApiResponse<any>>('/api/Foundation/CertificationBody/config')
}
