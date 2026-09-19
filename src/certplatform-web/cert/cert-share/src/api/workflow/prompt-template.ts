/**
 * Prompt 模板 API（YZH 标准端点封装）
 *
 * 路由前缀：/api/PromptTemplate
 * 后端控制器：PromptTemplateController（[Route("api/[controller]")]）
 */

import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse, PagedData, FilterRequest } from '@yzh-core'

/**
 * Prompt 模板实体（后端 PascalCase 直接映射）
 */
export interface PromptTemplate {
  Id: number
  Code: string
  PromptCode: string
  PromptName: string
  PromptType: string
  SkillTarget?: string
  Template?: string
  Description?: string
  Version: number
  IsActive: boolean
  Enable: boolean
  Status?: string
  IsValid: number
  IsDeleted: boolean
  CreateTime?: string
  CreateBy?: string
  UpdateTime?: string
  UpdateBy?: string
}

/**
 * 获取 Prompt 模板分页数据（标准 filter 端点）
 * 返回后端 PagedData 结构（Items/TotalCount/PageIndex/PageSize）
 */
export async function getPromptPage(params: FilterRequest): Promise<PagedData<PromptTemplate>> {
  const res = await yzhApi.post<ApiResponse<PagedData<PromptTemplate>>>('/api/PromptTemplate/filter', params)
  return res.data || { Items: [], TotalCount: 0, PageIndex: 1, PageSize: 20 }
}

/**
 * @deprecated 保留向后兼容：旧版 directory/index.vue 使用
 */
export async function getPromptList(filters?: Record<string, any>): Promise<PromptTemplate[]> {
  const res = await yzhApi.post<ApiResponse<PagedData<PromptTemplate>>>('/api/PromptTemplate/filter', {
    Page: 1,
    PageSize: 1000,
    SortField: '',
    SortOrder: '',
    Filters: filters ? Object.entries(filters).map(([Field, Value]) => ({ Field, Value, Operator: 'eq' })) : []
  })
  return res.data?.Items || []
}

/**
 * 保存 Prompt 模板（标准 add/update）
 */
export async function savePrompt(data: Partial<PromptTemplate>): Promise<any> {
  if (data.Id && data.Id > 0) {
    const res = await yzhApi.post<ApiResponse<any>>('/api/PromptTemplate/update', data)
    return res.data
  }
  const res = await yzhApi.post<ApiResponse<any>>('/api/PromptTemplate/add', data)
  return res.data
}

/**
 * 删除 Prompt 模板（标准 delete）
 */
export async function deletePrompt(codes: string[]): Promise<any> {
  const res = await yzhApi.post<ApiResponse<any>>('/api/PromptTemplate/delete', codes)
  return res.data
}

/**
 * 激活 Prompt 模板（业务操作）
 */
export async function activatePrompt(code: string): Promise<any> {
  const res = await yzhApi.post<ApiResponse<any>>(`/api/PromptTemplate/action/activate?code=${code}`)
  return res.data
}
