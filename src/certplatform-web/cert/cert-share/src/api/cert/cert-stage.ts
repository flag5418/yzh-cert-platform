import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse, PagedData } from '@share/types/contracts'
import type { CertStage } from '../../types/cert'

/** 获取认证阶段分页列表 */
export async function getCertStagePage(params: any): Promise<ApiResponse<PagedData<CertStage>>> {
  return yzhApi.post<ApiResponse<PagedData<CertStage>>>('/api/Foundation/CertStage/filter', params)
}

/** 获取认证阶段列表（不限分页，供下拉选择） */
export async function getCertStageList(): Promise<ApiResponse<CertStage[]>> {
  return yzhApi.post<ApiResponse<CertStage[]>>('/api/Foundation/CertStage/filter', { page: 1, pageSize: 1000 })
}

/** 新增认证阶段 */
export async function addCertStage(data: Partial<CertStage>): Promise<ApiResponse<CertStage>> {
  return yzhApi.post<ApiResponse<CertStage>>('/api/Foundation/CertStage/add', data)
}

/** 修改认证阶段 */
export async function updateCertStage(data: Partial<CertStage>): Promise<ApiResponse<CertStage>> {
  return yzhApi.post<ApiResponse<CertStage>>('/api/Foundation/CertStage/update', data)
}

/** 删除认证阶段 */
export async function deleteCertStage(codes: string[]): Promise<ApiResponse<object>> {
  return yzhApi.post<ApiResponse<object>>('/api/Foundation/CertStage/delete', codes)
}

/** 切换启用/停用 */
export async function toggleCertStageValid(code: string): Promise<ApiResponse<{ Code: string; IsValid: number }>> {
  return yzhApi.post<ApiResponse<{ Code: string; IsValid: number }>>('/api/Foundation/CertStage/toggle-valid', { Code: code })
}
