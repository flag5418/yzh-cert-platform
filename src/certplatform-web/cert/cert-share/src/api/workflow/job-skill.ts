import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from 'yzh.vue.core/types'

export interface SkillInput {
  inputName: string
  inputType: string
  isRequired: boolean
  defaultValue?: string
  description?: string
  bindMode?: string
  enumSource?: string
}

export interface SkillOutput {
  outputName: string
  outputType: string
  description?: string
}

export interface SkillReflection {
  classPath: string
  methodName: string
  paramBinding?: string
}

export interface Skill {
  id: number
  skillCode: string
  skillName: string
  description?: string
  category: string
  isActive: boolean
  inputs: SkillInput[]
  outputs: SkillOutput[]
  reflection: SkillReflection
}

export interface SkillCategory {
  id: number
  categoryCode: string
  categoryName: string
  icon?: string
  color?: string
  sortOrder: number
  enable: boolean
}

export interface AnalyzedSkill {
  code: string
  name: string
  returnType: string
  description: string
  inputPorts: SkillInput[]
  outputPorts: SkillOutput[]
}

export async function getSkillPage(params: PageParams, filters?: any): Promise<Page<Skill>> {
  return yzhApi.post<Page<Skill>>('/api/Skill/getPageData', params, { params: filters })
}

export async function getSkill(skillCode: string): Promise<Skill> {
  return yzhApi.get<Skill>(`/api/Skill/getDetail?skillCode=${skillCode}`)
}

export async function saveSkill(data: Skill): Promise<any> {
  return yzhApi.post('/api/Skill/save', data)
}

export async function deleteSkill(id: number): Promise<any> {
  return yzhApi.post(`/api/Skill/delete?id=${id}`)
}

export async function toggleSkillActive(id: number): Promise<any> {
  return yzhApi.post(`/api/Skill/toggleActive?id=${id}`)
}

export async function analyzeSkill(data: { classPath: string; methodName?: string }): Promise<AnalyzedSkill> {
  return yzhApi.post<AnalyzedSkill>('/api/Skill/analyze', data)
}

export async function getSkillCategories(): Promise<SkillCategory[]> {
  return yzhApi.post<SkillCategory[]>('/api/SkillCategory/getList')
}

export async function saveSkillCategory(data: SkillCategory): Promise<any> {
  return yzhApi.post('/api/SkillCategory/save', data)
}

export async function deleteSkillCategory(id: number): Promise<any> {
  return yzhApi.post(`/api/SkillCategory/delete?id=${id}`)
}

export async function toggleSkillCategoryActive(id: number): Promise<any> {
  return yzhApi.post(`/api/SkillCategory/toggleActive?id=${id}`)
}
