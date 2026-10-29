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

/** 文档提取提示词读取结果 */
export interface DocExtractPromptDto {
  promptCode: string
  standardCode?: string
  template: string
  isFallback: boolean
  placeholders: PlaceholderDescriptor[]
}

/** 文档提取提示词保存入参 */
export interface DocExtractPromptSaveRequest {
  template: string
  standardCode?: string
}

/**
 * 读取某标准的文档提取提示词。
 * <para>标准级（ScopeKey=标准Code）优先；缺失回落到全局默认（ScopeKey=空）。</para>
 * @param standardCode 标准短码（如 iso9001）；空 = 取全局默认
 */
export async function getPrompt(standardCode?: string): Promise<DocExtractPromptDto> {
  const query = standardCode ? `?standardCode=${encodeURIComponent(standardCode)}` : ''
  const res = await yzhApi.get<ApiResponse<DocExtractPromptDto>>(`${DOC_EXTRACT_RULE_BASE}/prompt${query}`)
  return res.data
}

/**
 * 保存某标准的文档提取提示词（幂等 upsert）。
 * @param data.template 提示词正文
 * @param data.standardCode 空 = 保存全局默认；非空 = 保存该标准的专属提示词
 */
export async function savePrompt(data: DocExtractPromptSaveRequest): Promise<void> {
  await yzhApi.post(`${DOC_EXTRACT_RULE_BASE}/prompt`, data)
}
