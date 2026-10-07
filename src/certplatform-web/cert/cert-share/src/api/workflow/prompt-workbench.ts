/**
 * 提示词工作台 API（2026-10-02）
 *
 * 后端：CertPlatform.Admin / PromptTemplateController（路由 /api/PromptTemplate）
 *
 * ★ 契约（务必按此读，混了就是「页面空着、零报错」）：
 *   · 信封：camelCase —— `res.success` / `res.err`（唯一判据 = success）
 *   · data：**camelCase**（后端 DTO 已逐字段显式 [JsonPropertyName]）
 *     - resolve / list → PromptTemplateDto：promptCode / promptName / template / isActive …
 *       ⛔ 已无 modelName / maxTokens / temperature / version（统一 AI 配置 + 不做版本管理）
 *     - generate       → prompt / durationMs / promptTokens / completionTokens
 *     - test           → success / message / promptText / rawOutput / jsonOutput /
 *                        files[].{fileName,success,message,markdownLength,markdown,markdownHead} /
 *                        cacheKey / cacheHit / validation.{valid,messages,normalizedJson}
 *     - standards      → code / standardCode / standardName / display
 *
 * ★ Markdown 复用（33 号 §八 勘误后）：
 *   上传转换成功后后端回传 `cacheKey`，前端存起来；下次改提示词重测时**只传 cacheKey 不传 files**，
 *   后端读 Redis 直接复用 Markdown（零转换）。缓存 8 小时；过期会报「测试缓存已过期」，重新上传即可。
 *
 * ★ 写操作一律走 expectOk（信封规范 22 L3 / F-1）：
 *   后端业务失败 = HTTP 200 + success:false，yzhApi 原样返回不抛 →
 *   不 expectOk 就会「静默失败」甚至假成功提示。
 */

import { yzhApi } from '@yzh-core/api/client'
import { expectOk } from '@yzh-core/utils/apiResponse'
import type { ApiResponse } from '@yzh-core/types'

// ==================== 类型定义（对齐后端 PromptTemplateController.cs） ====================

/** 提示词类型常量（值必须与后端 PromptWorkbenchService.Types 一致） */
export const PROMPT_TYPE = {
  /** 标题 / 分类提示词：输入文件清单 → 输出每个文件的类别 */
  Group: 'doc_group',
  /** 作用提示词：输入单文件 Markdown → 输出该文件的作用 */
  Content: 'doc_content',
  /** 元提示词：供「AI 自动生成 / 优化」按钮调用 */
  Generator: 'prompt_generator',
  /** 旧路径：字段 / 表格提取 */
  DocumentAnalysis: 'document_analysis'
} as const

export interface PromptTemplateDto {
  id: number
  promptCode: string
  promptName: string
  promptType: string
  skillTarget?: string | null
  /** ★ 存的是 cert_iso_standard.Code（GUID）；空 = 平台级默认（对所有标准生效） */
  standardCode?: string | null
  template?: string | null
  description?: string | null
  isActive: boolean
  status?: string | null
  creator?: string | null
  createTime?: string | null
  updateTime?: string | null
  // ⛔ 无 modelName / maxTokens / temperature（统一 AI 配置，读 cert_sys_config）
  // ⛔ 无 version（2026-10-02 裁决：提示词不做版本管理）
}

export interface StandardOptionDto {
  /** ★ 存库值（GUID） */
  code: string
  standardCode: string
  standardName: string
  display: string
  /** 启用状态（0/1，左树启用/禁用徽章用；后端已过滤 isValid=1） */
  isValid?: number
}

export interface PromptGenerateResultDto {
  success: boolean
  message: string
  /** 生成的提示词正文（★ 不落库） */
  prompt?: string | null
  durationMs: number
  promptTokens?: number | null
  completionTokens?: number | null
}

export interface ConvertLogDto {
  fileName: string
  success: boolean
  message?: string | null
  markdownLength: number
  /** ★ 转换成功时的**完整 Markdown 正文**（用于「看转换结果」面板，33 号 §八 勘误） */
  markdown?: string | null
  /** 开头 2000 字（后端兼容字段） */
  markdownHead?: string | null
}

/** ★ 33 号 §3.3 后端六条校验结果 */
export interface PromptValidationDto {
  /** false = 输出不是合法 JSON ⇒ **硬拦保存** */
  valid: boolean
  /** 每条对应一次后端修复（越界回退 OTHER / clamp / 截断 / 缺作用） */
  messages: string[]
  /** 校验修复后的 JSON（已回写 OTHER、clamp、截断） */
  normalizedJson?: string | null
}

export interface PromptTestResultDto {
  success: boolean
  message: string
  model?: string | null
  maxTokens?: number | null | undefined
  temperature?: number | null | undefined
  /** 实际送出的提示词全文（可确认占位符替换正确） */
  promptText?: string | null
  rawOutput?: string | null
  /** ForceJson 解析成功时的 JSON 文本 */
  jsonOutput?: string | null
  durationMs: number
  promptTokens?: number | null
  completionTokens?: number | null
  files: ConvertLogDto[]
  /** ★ 本次 Markdown 缓存键 —— 前端务必存下，下次重测只传它 */
  cacheKey?: string | null
  /** true = 这次没重新转换，直接读了 Redis */
  cacheHit?: boolean
  validation?: PromptValidationDto | null
}

export interface PromptSavePayload {
  promptCode: string
  promptName?: string
  promptType: string
  standardCode?: string | null
  skillTarget?: string | null
  template: string
  description?: string | null
  // ⛔ 无 modelName / maxTokens / temperature（统一 AI 配置，Q3=a）
}

// ==================== API 函数 ====================

/** 标准下拉（供选择提示词的适用标准） */
export async function getStandardOptions(): Promise<StandardOptionDto[]> {
  const res = await yzhApi.get<ApiResponse<StandardOptionDto[]>>(
    '/api/Admin/Workflow/PromptTemplate/workbench/standards'
  )
  expectOk(res, '加载标准列表失败')
  return res.data || []
}

/** 列出「某类型 + 某标准」下的提示词（含平台级 StandardCode 为空的行） */
export async function listPrompts(
  promptType: string,
  standardCode?: string | null
): Promise<PromptTemplateDto[]> {
  const res = await yzhApi.get<ApiResponse<PromptTemplateDto[]>>('/api/Admin/Workflow/PromptTemplate/workbench/list', {
    promptType,
    standardCode: standardCode || undefined
  })
  expectOk(res, '加载提示词列表失败')
  return res.data || []
}

/** 三层回退定位生效提示词（标准级 → 平台级） */
export async function resolveActivePrompt(
  promptType: string,
  standardCode?: string | null
): Promise<PromptTemplateDto | null> {
  const res = await yzhApi.get<ApiResponse<PromptTemplateDto>>('/api/Admin/Workflow/PromptTemplate/workbench/resolve', {
    promptType,
    standardCode: standardCode || undefined
  })
  // resolve 未命中时后端返 success:false —— 这不是错误，是「还没有提示词」，返回 null
  if (!res?.success) return null
  return res.data || null
}

/**
 * AI 生成 / 优化提示词草稿（★ 不落库，返回正文由用户确认后保存）。
 *
 * ★ `currentTemplate` 非空 = AI **优化**这段现有手写内容（Q4 两用，不全量重写）；
 *   空 = 按标准从零生成。
 */
export async function generatePromptDraft(params: {
  promptType: string
  standardCode?: string | null
  extraRequirement?: string | null
  currentTemplate?: string | null
}): Promise<PromptGenerateResultDto> {
  const res = await yzhApi.post<ApiResponse<PromptGenerateResultDto>>(
    '/api/Admin/Workflow/PromptTemplate/workbench/generate',
    {
      PromptType: params.promptType,
      StandardCode: params.standardCode || null,
      ExtraRequirement: params.extraRequirement || null,
      CurrentTemplate: params.currentTemplate || null
    }
  )
  expectOk(res, 'AI 生成失败')
  if (!res.data) throw new Error('AI 生成失败：未返回内容')
  return res.data
}

/**
 * 上传文件试跑提示词（multipart）。
 *
 * ★ 两条路径（后端 TestAsync 双分支）：
 *   ① `files` 非空 → 转 Markdown → 落 Redis（key 空则后端生成，回传到 `cacheKey`）
 *   ② `files` 为空 + `cacheKey` 有值 → **零转换**，直接读 Redis 复用 Markdown
 *   （⚠️ 缓存 8 小时；过期会返回「测试缓存已过期或不存在」，请重新上传文件）
 *
 * ★ 文件只落转换容器临时目录（后端 finally 已清理）——不落 MinIO、不落 DB、不留痕。
 * ★ `template` 传「页面上未保存的编辑内容」，实现真正的边改边试。
 * ⚠️ 试跑失败（success:false）时后端**仍返回完整结果**（含转换日志与实际提示词），
 *   所以这里**不 expectOk** —— 由调用方读 `data.success` 决定提示语，但结果照样展示。
 */
export async function testPrompt(params: {
  files?: File[]
  promptType: string
  template?: string | null
  standardCode?: string | null
  cacheKey?: string | null
}): Promise<PromptTestResultDto> {
  const fd = new FormData()
  ;(params.files || []).forEach((f) => fd.append('Files', f, f.name))
  fd.append('PromptType', params.promptType)
  if (params.template) fd.append('Template', params.template)
  if (params.standardCode) fd.append('StandardCode', params.standardCode)
  if (params.cacheKey) fd.append('CacheKey', params.cacheKey)

  const res = await yzhApi.upload<ApiResponse<PromptTestResultDto>>(
    '/api/Admin/Workflow/PromptTemplate/workbench/test',
    fd
  )
  if (!res) throw new Error('试跑失败：无响应')
  // 后端在 data 里带 success；信封失败（如参数缺失）时 data 为 null
  if (!res.data) throw new Error(res.err || res.message || '试跑失败')
  return res.data
}

/**
 * 保存提示词（按 PromptCode 幂等 upsert；⛔ 版本不再 +1）
 *
 * ⚠️ 请求体必须是 **PascalCase**：后端 `PromptSaveRequest` 无 `[JsonPropertyName]`，
 * 走 MVC 的 `PropertyNamingPolicy = null` ⇒ 用 camelCase 提交会**全部绑成 null**
 * （表现为「保存成功但内容全空」，零报错）。故此处显式逐字段转 PascalCase。
 */
export async function savePrompt(payload: PromptSavePayload): Promise<void> {
  const res = await yzhApi.post<ApiResponse<null>>('/api/Admin/Workflow/PromptTemplate/workbench/save', {
    PromptCode: payload.promptCode,
    PromptName: payload.promptName ?? null,
    PromptType: payload.promptType,
    StandardCode: payload.standardCode ?? null,
    SkillTarget: payload.skillTarget ?? null,
    Template: payload.template,
    Description: payload.description ?? null
  })
  expectOk(res, '保存提示词失败')
}

/** 删除提示词（逻辑禁用 IsValid = 0） */
export async function deletePrompt(promptCode: string): Promise<void> {
  const res = await yzhApi.post<ApiResponse<null>>('/api/Admin/Workflow/PromptTemplate/workbench/delete', null, {
    params: { code: promptCode }
  })
  expectOk(res, '删除提示词失败')
}

/** 设为生效（同类型其他提示词自动置为不生效） */
export async function activatePrompt(promptCode: string): Promise<void> {
  const res = await yzhApi.post<ApiResponse<null>>('/api/Admin/Workflow/PromptTemplate/workbench/activate', null, {
    params: { code: promptCode }
  })
  expectOk(res, '设为生效失败')
}
