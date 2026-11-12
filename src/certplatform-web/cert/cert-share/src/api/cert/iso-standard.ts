import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { ISOStandard } from '../../types/cert'

/** 获取 ISO 标准分页列表 */
export async function getISOStandardPage(params: PageParams): Promise<Page<ISOStandard>> {
  return yzhApi.post<Page<ISOStandard>>('/api/Admin/Foundation/ISOStandard/filter', params)
}

/** 新增 ISO 标准 */
export async function addISOStandard(data: Partial<ISOStandard>): Promise<any> {
  return yzhApi.post('/api/Admin/Foundation/ISOStandard/add', data)
}

/** 修改 ISO 标准 */
export async function updateISOStandard(data: Partial<ISOStandard>): Promise<any> {
  return yzhApi.post('/api/Admin/Foundation/ISOStandard/update', data)
}

/** 删除 ISO 标准（传 Code 数组） */
export async function deleteISOStandard(codes: string[]): Promise<any> {
  return yzhApi.post('/api/Admin/Foundation/ISOStandard/delete', codes)
}

/** 切换 ISO 标准启用/禁用 */
export async function toggleISOStandardValid(code: string): Promise<any> {
  return yzhApi.post('/api/Admin/Foundation/ISOStandard/toggle-valid', { Code: code })
}

/** 获取 ISO 标准下拉列表（供条款页面选择） */
export async function getISOStandardList(): Promise<{ rows: ISOStandard[] }> {
  return yzhApi.post<{ rows: ISOStandard[] }>('/api/Admin/Foundation/ISOStandard/filter', {
    page: 1,
    pageSize: 1000
  })
}


// ════════════════════════════════════════════════════════════════════
// ★ 标准原文 → Markdown → 条款（2026-11-10）
// ════════════════════════════════════════════════════════════════════

/** 解析出的一条条款 */
export interface StandardParsedClause {
  ClauseNumber: string
  Title: string
  Description?: string
}

/** 上传/预览返回的解析结果 */
export interface StandardSourcePreview {
  StandardCode: string
  StandardName: string
  MarkdownPath: string
  MarkdownChars: number
  SourceFile?: string
  ParsedCount: number
  ExistingCount: number
  /** markdown-heading = 按标题编号解析；llm = 模型兜底 */
  Parser: string
  Clauses: StandardParsedClause[]
}

/** 写入条款结果 */
export interface StandardClauseApplyResult {
  Success: boolean
  Message: string
  StandardCode: string
  Added: number
  Updated: number
  Skipped: number
  /** overwrite=true 时被软删除的旧条款数 */
  Cleared: number
  Total: number
}

/** 上传标准原文（PDF / DOCX）→ 转 Markdown 并返回条款预览（不写库） */
export async function uploadStandardMarkdown(
  file: File,
  standardCode: string
): Promise<{ success: boolean; message: string; err?: string; data?: StandardSourcePreview }> {
  const fd = new FormData()
  fd.append('File', file)
  fd.append('StandardCode', standardCode)
  return yzhApi.upload('/api/Admin/Foundation/ISOStandard/upload-markdown', fd)
}

/** 对已有 Markdown 的标准重新解析（不写库） */
export async function previewStandardClauses(standardCode: string): Promise<any> {
  return yzhApi.post('/api/Admin/Foundation/ISOStandard/preview-clauses', {
    StandardCode: standardCode,
  })
}

/** 按解析结果写入条款（Overwrite=false = 只补缺失编号） */
export async function applyStandardClauses(
  standardCode: string,
  overwrite = false
): Promise<any> {
  return yzhApi.post('/api/Admin/Foundation/ISOStandard/apply-clauses', {
    StandardCode: standardCode,
    Overwrite: overwrite,
  })
}
