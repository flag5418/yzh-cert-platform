import { yzhApi } from '@yzh-core/api/client'
import { expectOk } from '@yzh-core/utils/apiResponse'
import type { ApiResponse } from '@yzh-core/types'

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
  stageCode: string
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
//
// ★ 写操作/分析类调用一律 expectOk（信封规范 22 L3 / F-1）：
//   后端业务失败 = HTTP 200 + success:false，yzhApi 原样返回不抛 →
//   不 expectOk 就会「静默失败」甚至假成功提示。失败统一抛 BizError，
//   由页面 catch 弹出信封 err 文本（F-3 谁 catch 谁弹）。

export const getRuleDetail = (standardFileCode: string) =>
  yzhApi.get<{ code: number; data: RuleDetailResponse }>(`/api/Admin/Workflow/DocExtractionRule/${standardFileCode}`)

export async function saveRule(data: {
  fileCode: string
  orgCode?: string
  standardCode?: string
  stageCode?: string
  skill?: string
  fields: FieldDefDto[]
  tables: TableDefDto[]
  prompt: string
  isValid: boolean
  extractionData?: ExtractionData
}): Promise<ApiResponse<null>> {
  const res = await yzhApi.post<ApiResponse<null>>('/api/Admin/Workflow/DocExtractionRule/save', data)
  expectOk(res, '保存规则失败')
  return res
}

export async function deleteRule(standardFileCode: string): Promise<ApiResponse<null>> {
  const res = await yzhApi.post<ApiResponse<null>>(`/api/Admin/Workflow/DocExtractionRule/${standardFileCode}/delete`)
  expectOk(res, '删除规则失败')
  return res
}

export interface ConfiguredRuleItem {
  ruleCode: string
  standardFileCode: string
  fileName: string
  standardCode?: string
  stageCode?: string
  skill?: string
  isValid?: boolean
}

export const getConfiguredRules = () =>
  yzhApi.get<{ code: number; data: ConfiguredRuleItem[] }>('/api/Admin/Workflow/DocExtractionRule/configured-rules')

export const getFieldsAndTables = (ruleCode: string) =>
  yzhApi.get<{ code: number; data: { fields: FieldDefDto[]; tables: TableDefDto[] } }>(
    `/api/Admin/Workflow/DocExtractionRule/${ruleCode}/fields-tables`
  )

// --- AI 功能 ---

export async function analyzeDoc(
  fileCode: string,
  skill?: string,
): Promise<ApiResponse<{ fields: FieldDefDto[]; tables: TableDefDto[]; message: string }>> {
  const res = await yzhApi.post<ApiResponse<{ fields: FieldDefDto[]; tables: TableDefDto[]; message: string }>>(
    '/api/Admin/Workflow/DocExtractionRule/analyze',
    { fileCode, skill },
  )
  expectOk(res, 'AI 分析失败')
  return res
}

export async function generatePrompt(
  data: { fileCode: string; fields: FieldDefDto[]; tables: TableDefDto[] },
): Promise<ApiResponse<string>> {
  const res = await yzhApi.post<ApiResponse<string>>('/api/Admin/Workflow/DocExtractionRule/generate-prompt', data)
  expectOk(res, '生成 Prompt 失败')
  return res
}

export async function verifyPrompt(
  data: { fileCode: string; prompt: string },
): Promise<ApiResponse<VerifyResult>> {
  const res = await yzhApi.post<ApiResponse<VerifyResult>>('/api/Admin/Workflow/DocExtractionRule/verify', data)
  expectOk(res, 'Prompt 验证失败')
  return res
}

export async function testField(
  data: { ruleCode: string; fieldCode: string; docType?: string },
): Promise<ApiResponse<{ fieldCode: string; value: unknown; confidence: number; message: string }>> {
  const res = await yzhApi.post<ApiResponse<{ fieldCode: string; value: unknown; confidence: number; message: string }>>(
    '/api/Admin/Workflow/DocExtractionRule/test-field',
    data,
  )
  expectOk(res, '字段测试失败')
  return res
}

export async function testTable(
  data: { ruleCode: string; tableCode: string; docType?: string },
): Promise<ApiResponse<{ tableCode: string; rows: Record<string, unknown>[]; confidence: number; message: string }>> {
  const res = await yzhApi.post<ApiResponse<{ tableCode: string; rows: Record<string, unknown>[]; confidence: number; message: string }>>(
    '/api/Admin/Workflow/DocExtractionRule/test-table',
    data,
  )
  expectOk(res, '表格测试失败')
  return res
}

// --- AI 配置 ---

export const getAIConfig = () =>
  yzhApi.get<{ code: number; data: AIConfigDto }>('/api/Admin/Workflow/DocExtractionRule/ai-config')

export async function updateAIConfig(config: AIConfigDto): Promise<ApiResponse<null>> {
  const res = await yzhApi.post<ApiResponse<null>>('/api/Admin/Workflow/DocExtractionRule/ai-config', config)
  expectOk(res, '保存 AI 配置失败')
  return res
}

export const getSkills = () =>
  yzhApi.get<{ code: number; data: SkillInfo[] }>('/api/Admin/Workflow/DocExtractionRule/skills')

// --- 文件内容（D-6 双产物） ---

/**
 * 预览 PDF 流（带鉴权取回 Blob）
 *
 * ⚠️ 不能用 `<iframe src="...file-preview?...">` 直接渲染：
 *   本平台 JWT 走 Authorization 头，iframe/img 无法携带 → 必然 401。
 *   必须先 getBlob 取回字节，再用 ObjectURL 交给渲染器。
 */
export const getFilePreviewBlob = (fileCode: string) =>
  yzhApi.getBlob('/api/Admin/Workflow/DocExtractionRule/file-preview', { fileCode })

/**
 * ★ 按**存储路径**取预览 PDF 流（2026-10-04 新增）。
 *
 * 【为什么需要第二条预览链】
 *   `file-preview` 按 `fileCode` 查 `cert_standard_directory_file`。
 *   而**空白模板**存在 `…/_template/xxx.docx`，在标准目录表里**没有对应行**，
 *   模板表也没有 `FileCode` ⇒ 那条链**根本查不到它**，只能吃裸路径。
 *
 * 【缓存语义】
 *   服务端把转换结果缓存到源路径派生的 `…/pdf/x.docx.pdf`（与标准目录产物同一套口径）。
 *   因此**换版（重新上传同名模板）必须让服务端先失效缓存**，否则会看到上一版的 PDF
 *   —— 上传接口已内建该失效动作，前端无需传 `useCache:false`。
 *
 * ⚠️ `fileName` 必须传**真实文件名（含扩展名）**：服务端靠它判扩展名与推导产物名。
 *   只给路径的话，`…/editable/陪审人员.doc.docx` 这类「双重扩展名」产物仍能正确取到
 *   `.docx`，但模板路径一旦不带扩展名就会误判为不支持预览。
 */
export const getPreviewBlobByPath = (storagePath: string, fileName?: string) =>
  yzhApi.getBlob('/api/Admin/Workflow/DocExtractionRule/preview-by-path', {
    storagePath,
    ...(fileName ? { fileName } : {}),
  })

export const getFileMarkdown = (fileCode: string) =>
  yzhApi.get<{ code: number; data: string }>(`/api/Admin/Workflow/DocExtractionRule/file-markdown?fileCode=${fileCode}`)
