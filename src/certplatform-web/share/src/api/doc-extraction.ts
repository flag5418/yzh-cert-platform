import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface DocExtractionRule {
  id: number
  ruleCode: string
  fileCode: string
  fileName: string
  status: 'none' | 'configured' | 'failed'
  fields?: DocFieldDef[]
  tables?: DocTableDef[]
  prompt?: string
  createDate?: string
}

export interface DocFieldDef {
  fieldCode: string
  fieldName: string
  dataType: string
  isRequired: boolean
  sortOrder: number
}

export interface DocTableDef {
  tableCode: string
  tableName: string
  description?: string
  sortOrder: number
  fields?: DocFieldDef[]
}

export interface DocAnalysisResult {
  fields: DocFieldDef[]
  tables: DocTableDef[]
}

export async function getDocRuleList(params: PageParams & { fileCode?: string }): Promise<DocExtractionRule> {
  return yzhApi.post<DocExtractionRule>('/api/DocExtraction/Rule/getList', params)
}

export async function createDocRule(fileCode: string): Promise<DocExtractionRule> {
  return yzhApi.post<DocExtractionRule>('/api/DocExtraction/Rule/create', { fileCode })
}

export async function analyzeDocWithAI(fileCode: string, fileType: string): Promise<DocAnalysisResult> {
  return yzhApi.post<DocAnalysisResult>('/api/DocExtraction/Rule/analyze', { fileCode, fileType })
}

export async function saveDocRule(data: Partial<DocExtractionRule>): Promise<any> {
  return yzhApi.post('/api/DocExtraction/Rule/save', data)
}

export async function getDocContent(fileCode: string): Promise<{ content: string; previewUrl?: string }> {
  return yzhApi.get<{ content: string; previewUrl?: string }>(`/api/DocExtraction/File/content?fileCode=${fileCode}`)
}
