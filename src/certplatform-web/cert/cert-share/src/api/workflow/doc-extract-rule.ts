/**
 * 文档提取规则 API（后台管理）
 *
 * 每标准一条规则：该国标 Markdown 知识库 + 提取提示词
 * 后端：DocExtractRuleController（/api/Admin/Workflow/DocExtractRule）
 *
 * ⛔ 不改造也不复用 /business/prompt-template —— 那个页面是 Dify 工作流模板绑定，变量体系完全不同
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse, FilterRequest, PagedResult } from '@yzh-core'

export const DOC_EXTRACT_RULE_BASE = '/api/Admin/Workflow/DocExtractRule'

// ── 实体类型 ──

export interface DocExtractRuleDto {
  Id: number
  Code: string
  StandardCode: string
  StandardName: string
  MarkdownContent: string | null
  PromptContent: string | null
  PromptVariables: string | null
  Version: number
  IsPublished: number
  Sort: number
  IsValid: number
  Remark: string | null
  Status: string | null
  CreateTime: string
  UpdateTime: string | null
}

// ── CRUD 端点 ★ 全是标准端点，三层同构 ──

/** 分页查询（左栏数据源） */
export async function filterRules(params: FilterRequest): Promise<PagedResult<DocExtractRuleDto>> {
  const res = await yzhApi.post<ApiResponse<PagedResult<DocExtractRuleDto>>>(`${DOC_EXTRACT_RULE_BASE}/filter`, params)
  return res.data
}

/** 新增 */
export async function addRule(entity: Partial<DocExtractRuleDto>): Promise<DocExtractRuleDto> {
  const res = await yzhApi.post<ApiResponse<DocExtractRuleDto>>(`${DOC_EXTRACT_RULE_BASE}/add`, entity)
  return res.data
}

/** 修改 */
export async function updateRule(entity: Partial<DocExtractRuleDto>): Promise<DocExtractRuleDto> {
  const res = await yzhApi.post<ApiResponse<DocExtractRuleDto>>(`${DOC_EXTRACT_RULE_BASE}/update`, entity)
  return res.data
}

/** 删除 */
export async function deleteRules(codes: string[]): Promise<void> {
  await yzhApi.post(`${DOC_EXTRACT_RULE_BASE}/delete`, codes)
}

/** 获取配置 */
export async function getRuleConfig(): Promise<any> {
  const res = await yzhApi.get<ApiResponse<any>>(`${DOC_EXTRACT_RULE_BASE}/config`)
  return res.data
}

// ── 业务端点 ──

/**
 * 加载单条规则详情（含 MarkdownContent + PromptContent 全文）
 * 列表不返全文（太长），点击编辑时按需加载。
 * ★ 走 /detail/{code} 专属端点，绕过 OnQueried 的列表脱敏。
 */
export async function getRuleDetail(code: string): Promise<DocExtractRuleDto | undefined> {
  const res = await yzhApi.get<ApiResponse<DocExtractRuleDto>>(`${DOC_EXTRACT_RULE_BASE}/detail/${encodeURIComponent(code)}`)
  return res.data
}

/** 保存（标准 /update 端点即可；暂不新增 /save 端点） */
export async function saveRule(entity: Partial<DocExtractRuleDto>): Promise<DocExtractRuleDto> {
  return updateRule(entity)
}

/**
 * 测试：上传文档 → LLM → 返回 JSON（不落库）
 *
 * ⚠️ Phase B 暂不实现（上传 + LLM 调用链路长）
 *    留接口供 Phase C 填充
 */
export async function testRule(_code: string, _file: File): Promise<any> {
  // TODO: Phase C 实现
  throw new Error('testRule 待 Phase C 实现')
}

// ── 提示词维护（2026-10-29 统一：cert_prompt_template） ──

/** 占位符描述符 */
export interface PlaceholderDescriptor {
  token: string
  description: string
}

/** 文档提取提示词读取结果（PascalCase —— C# 默认 JSON 序列化） */
export interface DocExtractPromptDto {
  PromptCode: string
  StandardCode?: string
  Template: string
  IsFallback: boolean
  Placeholders: PlaceholderDescriptor[]
}

/** 文档提取提示词保存入参 */
export interface DocExtractPromptSaveRequest {
  template: string
  standardCode?: string
  /** 提示词代码；缺省 = doc_clause_extract */
  promptCode?: string
}

/**
 * 读取某标准的文档提取提示词。
 * <para>标准级（ScopeKey=标准Code）优先；缺失回落到全局默认（ScopeKey=空）。</para>
 * @param standardCode 标准短码（如 iso9001）；空 = 取全局默认
 * @param promptCode 提示词代码；缺省 = doc_clause_extract（条款逐条）
 */
export async function getPrompt(standardCode?: string, promptCode?: string): Promise<DocExtractPromptDto> {
  const params = new URLSearchParams()
  if (standardCode) params.set('standardCode', standardCode)
  if (promptCode) params.set('promptCode', promptCode)
  const query = params.toString() ? `?${params.toString()}` : ''
  const res = await yzhApi.get<ApiResponse<DocExtractPromptDto>>(`${DOC_EXTRACT_RULE_BASE}/prompt${query}`)
  return res.data
}

/**
 * 保存某标准的文档提取提示词（幂等 upsert）。
 * @param data.template 提示词正文
 * @param data.standardCode 空 = 保存全局默认；非空 = 保存该标准的专属提示词
 */
export async function savePrompt(data: DocExtractPromptSaveRequest): Promise<void> {
  // ★ 必须 PascalCase：后端 DocExtractPromptSaveRequest 无 [JsonPropertyName]，
  //   传 camelCase（template/standardCode）会全绑 null ⇒ 报「必填字段为空」且零提示。
  await yzhApi.post(`${DOC_EXTRACT_RULE_BASE}/prompt`, {
    Template: data.template,
    StandardCode: data.standardCode ?? '',
    PromptCode: data.promptCode ?? 'doc_clause_extract',
  })
}

// ── 提示词类型目录（2026-11-25：与运行时提取路由同一事实源） ──

/** 可选提示词类型（页面下拉数据源） */
export interface DocExtractPromptTypeDto {
  PromptCode: string
  /** 对应的画像 ExtractionMode（画像总闸为空串） */
  Mode: string
  DisplayName: string
  Group: string
  Sort: number
}

/**
 * 读取可选提示词类型。
 * ★ 数据源 = 后端 DocExtractionPromptCatalog（与 Auditor 运行时路由同一映射），前端不再硬编码，杜绝漂移。
 */
export async function getPromptTypes(): Promise<DocExtractPromptTypeDto[]> {
  const res = await yzhApi.get<ApiResponse<DocExtractPromptTypeDto[]>>(`${DOC_EXTRACT_RULE_BASE}/prompt/types`)
  return res.data ?? []
}

// ── 页面内试跑（2026-11-10）──────────────────────────────────────────────

/** 试跑结果（后端 DocExtractRuleController.TestPrompt） */
export interface DocExtractTestResult {
  Success: boolean
  Message: string
  Model: string
  DurationMs: number
  PromptTokens: number
  CompletionTokens: number
  /** 输出是不是合法 JSON（只判形状，不判内容对错） */
  JsonValid: boolean
  /** 模型原始输出（已去 ``` 围栏） */
  Output: string
  PromptChars: number
  ClauseCount: number
  MarkdownLength: number
  /** 文档超 3 万字被截断（只取前 3 万送模型） */
  Truncated: boolean
  MarkdownHead: string
  FileName: string
  /** 试跑用的提示词 Code（doc_profile / doc_extract_xxx / doc_clause_extract） */
  Mode?: string
  AllClauseCount?: number
  SkippedByDocCheckMode?: number
  Note?: string | null
  /** 画像整链：doc_profile 试跑时续跑得到的画像 JSON 原文（供核对分流），未续跑也有值 */
  ProfileOutput?: string | null
  /** 画像整链：画像判定的 ExtractionMode（如 manual_chapter） */
  RoutedMode?: string | null
  /** 画像整链：自动续跑用的「按类型提取」提示词 Code（如 doc_extract_manual_chapter） */
  RoutedPromptCode?: string | null
}

/**
 * 上传文档 → 转 Markdown → 用「页面当前正文」跑一次（不落库，全文口径）。
 * @param params.template 页面上未保存的编辑内容（边改边试）
 * @param params.promptCode 当前提示词 Code，后端据此按家族渲染占位符
 */
export async function testDocExtractPrompt(params: {
  file: File
  standardCode: string
  template: string
  promptCode: string
}): Promise<DocExtractTestResult> {
  const fd = new FormData()
  fd.append('File', params.file, params.file.name)
  fd.append('StandardCode', params.standardCode)
  fd.append('Template', params.template)
  fd.append('PromptCode', params.promptCode)

  const res = await yzhApi.upload<ApiResponse<DocExtractTestResult>>(`${DOC_EXTRACT_RULE_BASE}/prompt/test`, fd)
  if (!res) throw new Error('试跑失败：无响应')
  if (!res.data) throw new Error(res.err || res.message || '试跑失败')
  return res.data
}
