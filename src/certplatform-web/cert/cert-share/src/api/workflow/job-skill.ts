import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse, PagedData } from 'yzh.vue.core/types'

export interface Skill {
  id?: number
  Code?: string
  Name: string
  Description?: string
  CategoryCode?: string
  SkillType: string
  PromptTemplate?: string
  SortOrder: number
  IsValid: number
}

export interface SkillCategory {
  id?: number
  Code?: string
  Name: string
  Description?: string
  Icon?: string
  Color?: string
  SortOrder: number
  IsValid: number
}

/** 技能分页查询 */
export async function getSkillPage(params: any): Promise<ApiResponse<PagedData<Skill>>> {
  return yzhApi.post<ApiResponse<PagedData<Skill>>>('/api/Workflow/WfSkill/filter', params)
}

/** 新增技能 */
export async function addSkill(data: Partial<Skill>): Promise<ApiResponse<Skill>> {
  return yzhApi.post<ApiResponse<Skill>>('/api/Workflow/WfSkill/add', data)
}

/** 修改技能 */
export async function updateSkill(data: Partial<Skill>): Promise<ApiResponse<Skill>> {
  return yzhApi.post<ApiResponse<Skill>>('/api/Workflow/WfSkill/update', data)
}

/** 删除技能 */
export async function deleteSkill(codes: string[]): Promise<ApiResponse<object>> {
  return yzhApi.post<ApiResponse<object>>('/api/Workflow/WfSkill/delete', codes)
}

/** 切换启用/停用 */
export async function toggleSkillValid(code: string): Promise<ApiResponse<{ Code: string; IsValid: number }>> {
  return yzhApi.post<ApiResponse<{ Code: string; IsValid: number }>>('/api/Workflow/WfSkill/toggle-valid', { Code: code })
}

/** 技能分类分页查询 */
export async function getSkillCategoryPage(params: any): Promise<ApiResponse<PagedData<SkillCategory>>> {
  return yzhApi.post<ApiResponse<PagedData<SkillCategory>>>('/api/Workflow/WfSkillCategory/filter', params)
}

/** 新增技能分类 */
export async function addSkillCategory(data: Partial<SkillCategory>): Promise<ApiResponse<SkillCategory>> {
  return yzhApi.post<ApiResponse<SkillCategory>>('/api/Workflow/WfSkillCategory/add', data)
}

/** 修改技能分类 */
export async function updateSkillCategory(data: Partial<SkillCategory>): Promise<ApiResponse<SkillCategory>> {
  return yzhApi.post<ApiResponse<SkillCategory>>('/api/Workflow/WfSkillCategory/update', data)
}

/** 删除技能分类 */
export async function deleteSkillCategory(codes: string[]): Promise<ApiResponse<object>> {
  return yzhApi.post<ApiResponse<object>>('/api/Workflow/WfSkillCategory/delete', codes)
}

/** 切换分类启用/停用 */
export async function toggleSkillCategoryValid(code: string): Promise<ApiResponse<{ Code: string; IsValid: number }>> {
  return yzhApi.post<ApiResponse<{ Code: string; IsValid: number }>>('/api/Workflow/WfSkillCategory/toggle-valid', { Code: code })
}
