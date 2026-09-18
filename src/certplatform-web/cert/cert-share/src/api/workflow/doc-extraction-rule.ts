import { yzhApi } from '@yzh-core/api/client'

// ==================== 类型定义（对齐后端 DocExtractionDtos.cs） ====================

export interface FieldDefDto {
  name: string
  nameEn?: string
  code: string
  dataType: string
  description?: string
  isRequired: boolean
  isManual: boolean
  isAiRecommended: boolean
  extractedValue?: string
}

export interface TableColumnDto {
  name: string
  nameEn?: string
  code: string
  dataType: string
  isRequired: boolean
}

export interface TableDefDto {
  name: string
  nameEn?: string
  code: string
  description?: string
  sheetName?: string
  columns: TableColumnDto[]
  isAiRecommended: boolean
  extractedData?: Record<string, unknown>[]
}

export interface ExtractionData {
  fields?: Record<string, unknown>
  tables?: Record<string, Record<string, unknown>[]>
  message?: string
}

export interface RuleDetailResponse {
  id: number
  code: string
  standardFileCode: string
  orgCode: string
  standardCode: string
  phaseCode: string
  skill: string
  prompt?: string
  isValid: boolean
  status: 'none' | 'configured' | 'failed'
  fields: FieldDefDto[]
  tables: TableDefDto[]
  createDate?: string
  modifyDate?: string
}

export interface AIConfigDto {
  provider: string
  apiKey: string
  model: string
  temperature: number
  maxTokens: number
}

export interface SkillInfo {
  code: string
  name: string
  description: string
  supportedExtensions: string[]
}

export interface VerifyResult {
  success: boolean
  message: string
  data?: ExtractionData
}

// ==================== API 函数 ====================

// --- 规则 CRUD ---

export const getRuleDetail = (standardFileCode: string) =>
  yzhApi.get<{ code: number; data: RuleDetailResponse }>(`/api/Workflow/DocExtractionRule/${standardFileCode}`)

export const saveRule = (data: {
  fileCode: string
  orgCode?: string
  standardCode?: string
  phaseCode?: string
  skill?: string
  fields: FieldDefDto[]
  tables: TableDefDto[]
  prompt: string
  isValid: boolean
  extractionData?: ExtractionData
}) => yzhApi.post<{ code: number; message: string }>('/api/Workflow/DocExtractionRule/save', data)

export const deleteRule = (standardFileCode: string) =>
  yzhApi.post<{ code: number; message: string }>(`/api/Workflow/DocExtractionRule/${standardFileCode}/delete`)

export interface ConfiguredRuleItem {
  ruleCode: string
  standardFileCode: string
  fileName: string
  standardCode?: string
  phaseCode?: string
  skill?: string
  isValid?: boolean
}

export const getConfiguredRules = () =>
  yzhApi.get<{ code: number; data: ConfiguredRuleItem[] }>('/api/Workflow/DocExtractionRule/configured-rules')

export const getFieldsAndTables = (ruleCode: string) =>
  yzhApi.get<{ code: number; data: { fields: FieldDefDto[]; tables: TableDefDto[] } }>(
    `/api/Workflow/DocExtractionRule/${ruleCode}/fields-tables`
  )

// --- AI 功能 ---

export const analyzeDoc = (fileCode: string, skill?: string) =>
  yzhApi.post<{ code: number; data: { fields: FieldDefDto[]; tables: TableDefDto[]; message: string } }>(
    '/api/Workflow/DocExtractionRule/analyze',
    { fileCode, skill }
  )

export const generatePrompt = (data: { fileCode: string; fields: FieldDefDto[]; tables: TableDefDto[] }) =>
  yzhApi.post<{ code: number; data: string }>('/api/Workflow/DocExtractionRule/generate-prompt', data)

export const verifyPrompt = (data: { fileCode: string; prompt: string }) =>
  yzhApi.post<{ code: number; data: VerifyResult }>(
    '/api/Workflow/DocExtractionRule/verify',
    data
  )

export const testField = (data: { ruleCode: string; fieldCode: string; docType?: string }) =>
  yzhApi.post<{ code: number; data: { fieldCode: string; value: unknown; confidence: number; message: string } }>(
    '/api/Workflow/DocExtractionRule/test-field',
    data
  )

export const testTable = (data: { ruleCode: string; tableCode: string; docType?: string }) =>
  yzhApi.post<{ code: number; data: { tableCode: string; rows: Record<string, unknown>[]; confidence: number; message: string } }>(
    '/api/Workflow/DocExtractionRule/test-table',
    data
  )

// --- AI 配置 ---

export const getAIConfig = () =>
  yzhApi.get<{ code: number; data: AIConfigDto }>('/api/Workflow/DocExtractionRule/ai-config')

export const updateAIConfig = (config: AIConfigDto) =>
  yzhApi.post<{ code: number; message: string }>('/api/Workflow/DocExtractionRule/ai-config', config)

export const getSkills = () =>
  yzhApi.get<{ code: number; data: SkillInfo[] }>('/api/Workflow/DocExtractionRule/skills')

// --- 文件内容（D-6 双产物） ---

/**
 * 预览 PDF 流（带鉴权取回 Blob）
 *
 * ⚠️ 不能用 `<iframe src="...file-preview?...">` 直接渲染：
 *   本平台 JWT 走 Authorization 头，iframe/img 无法携带 → 必然 401。
 *   必须先 getBlob 取回字节，再用 ObjectURL 交给渲染器。
 */
export const getFilePreviewBlob = (fileCode: string) =>
  yzhApi.getBlob('/api/Workflow/DocExtractionRule/file-preview', { fileCode })

export const getFileMarkdown = (fileCode: string) =>
  yzhApi.get<{ code: number; data: string }>(`/api/Workflow/DocExtractionRule/file-markdown?fileCode=${fileCode}`)
