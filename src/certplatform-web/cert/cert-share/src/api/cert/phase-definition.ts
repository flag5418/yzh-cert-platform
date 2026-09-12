import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse, PagedData } from '@share/types/contracts'
import type { PhaseDefinition } from '../../types/cert'

/** 获取认证阶段分页列表 */
export async function getPhaseDefinitionPage(params: any): Promise<ApiResponse<PagedData<PhaseDefinition>>> {
  return yzhApi.post<ApiResponse<PagedData<PhaseDefinition>>>('/api/Foundation/PhaseDefinition/filter', params)
}

/** 获取认证阶段列表（不限分页，供下拉选择） */
export async function getPhaseDefinitionList(): Promise<ApiResponse<PhaseDefinition[]>> {
  return yzhApi.post<ApiResponse<PhaseDefinition[]>>('/api/Foundation/PhaseDefinition/filter', { page: 1, pageSize: 1000 })
}

/** 新增认证阶段 */
export async function addPhaseDefinition(data: Partial<PhaseDefinition>): Promise<ApiResponse<PhaseDefinition>> {
  return yzhApi.post<ApiResponse<PhaseDefinition>>('/api/Foundation/PhaseDefinition/add', data)
}

/** 修改认证阶段 */
export async function updatePhaseDefinition(data: Partial<PhaseDefinition>): Promise<ApiResponse<PhaseDefinition>> {
  return yzhApi.post<ApiResponse<PhaseDefinition>>('/api/Foundation/PhaseDefinition/update', data)
}

/** 删除认证阶段 */
export async function deletePhaseDefinition(codes: string[]): Promise<ApiResponse<object>> {
  return yzhApi.post<ApiResponse<object>>('/api/Foundation/PhaseDefinition/delete', codes)
}

/** 切换启用/停用 */
export async function togglePhaseDefinitionValid(code: string): Promise<ApiResponse<{ Code: string; IsValid: number }>> {
  return yzhApi.post<ApiResponse<{ Code: string; IsValid: number }>>('/api/Foundation/PhaseDefinition/toggle-valid', { Code: code })
}
