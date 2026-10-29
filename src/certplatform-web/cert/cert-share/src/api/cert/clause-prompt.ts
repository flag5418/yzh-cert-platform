/**
 * 条款属性通用提示词维护 API（PromptType = clause_attr）
 *
 * ★ 后端：`PromptTemplateController`（`/api/Admin/Workflow/PromptTemplate`）。
 *   提示词工作台端点（`workbench/*`）：
 *   - `workbench/list?promptType=clause_attr` — 列出所有条款属性提示词（含平台级）
 *   - `workbench/save`                          — 保存（幂等 upsert：按 PromptCode 分流）
 *   - `workbench/delete?code=xxx`               — 逻辑删除
 *   - `workbench/activate?code=xxx`             — 设生效
 *   - `workbench/generate`                      — AI 生成草稿（不落库）
 *   - `workbench/standards`                     — 标准下拉（Code=GUID / StandardCode=可读短码 / Display=展示名）
 *
 * ★ 为什么不用 `cert/clause-attr.ts` 里复用 ClauseAttrController：
 *   条款属性提示词是**全局共享**资源（按标准隔离、按类型复用），不是某一条条款的附属数据。
 *   它的生命周期、检索口径（IsActive / StandardCode 回退）都与 `wf_prompt_template` 完全对齐，
 *   跟 `cert_clause_attr`（一条=某条款上的某属性，稀疏行）是完全不同的实体。
 *   ⇒ 走 PromptTemplate 工作台端点，与文档语义规则（33 号）的提示词同源管理。
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse } from '@yzh-core'
import { unwrap } from '@yzh-core'

/** 提示词工作台控制器路由前缀 */
export const CLAUSE_PROMPT_BASE = '/api/Admin/Workflow/PromptTemplate'

/** 条款属性提示词类型常量（与后端 `PromptTypes.ClauseAttr` 一致） */
export const CLAUSE_PROMPT_TYPE = 'clause_attr'

/** 一条提示词记录（★ 与后端 PromptTemplateDto 字段对齐，camelCase） */
export interface ClausePromptItem {
  /** 自增主键 */
  id: number
  /** 业务唯一键（如 clause_attr_platform / clause_attr_iso9001） */
  promptCode: string
  /** 展示名（如「条款属性预生成提示词（平台级）」） */
  promptName: string
  /** 提示词类型（固定 clause_attr） */
  promptType: string
  /** 适用标准 GUID；空 = 平台级默认 */
  standardCode: string | null
  /** 提示词正文（含占位符） */
  template: string
  /** 说明（可选） */
  description: string | null
  /** 是否生效（同类型只允许一条生效） */
  isActive: boolean
  /** 状态文本（active / draft …） */
  status: string | null
  /** 创建人 */
  creator: string | null
  /** 创建时间 */
  createTime: string | null
  /** 更新时间 */
  updateTime: string | null
}

/** 标准下拉项 */
export interface ClausePromptStandard {
  /** GUID（用于 standardCode 字段读写） */
  code: string
  /** 可读短码 */
  standardCode: string | null
  /** 展示名 */
  standardName: string | null
  /** ISO 展示名（「ISO 9001（iso9001）」） */
  display: string
}

/** 保存请求体 */
export interface ClausePromptSaveBody {
  /** 业务唯一键（新增时由前端生成：`clause_attr_{standardCode}` 或 `clause_attr_platform`） */
  promptCode: string
  /** 展示名 */
  promptName: string
  /** 提示词类型（固定 clause_attr） */
  promptType: string
  /** 适用标准 GUID；空 = 平台级 */
  standardCode?: string | null
  /** 提示词正文 */
  template: string
  /** 说明 */
  description?: string | null
}

/** AI 生成请求体 */
export interface ClausePromptGenerateBody {
  /** 固定 clause_attr */
  promptType: string
  /** 适用标准 GUID */
  standardCode?: string | null
  /** 额外要求 */
  extraRequirement?: string
  /** 现有正文（非空 = 优化模式） */
  currentTemplate?: string
}

/** AI 生成结果 */
export interface ClausePromptGenerateResult {
  success: boolean
  message: string
  /** 警告（草稿已生成但需人工完善） */
  warning?: string
  /** 生成的提示词正文 */
  prompt?: string
  durationMs: number
  promptTokens?: number
  completionTokens?: number
}

/**
 * 列出所有条款属性提示词（含平台级 —— standardCode 为空的行）。
 *
 * <p>⚠️ 一次全量返回（不分页）：条款属性提示词条数极少（每标准最多 1 + 1 个平台级），
 * 全量返回比分页更直观，用户一眼看到「这个标准配了没有」。</p>
 */
export async function listClausePrompts(): Promise<ClausePromptItem[]> {
  const res = await yzhApi.get<ApiResponse<ClausePromptItem[]>>(
    `${CLAUSE_PROMPT_BASE}/workbench/list`,
    { params: { promptType: CLAUSE_PROMPT_TYPE } },
  )
  return unwrap(res, [])
}

/**
 * 保存一条条款属性提示词（按 PromptCode 幂等 upsert）。
 *
 * <p>新增 / 修改都走这个端点：无记录插一条、有记录覆盖正文并置为生效。
 * 版本管理已裁决不做（见后端 `SaveAsync`）。</p>
 */
export async function saveClausePrompt(body: ClausePromptSaveBody): Promise<string> {
  const res = await yzhApi.post<ApiResponse<string>>(
    `${CLAUSE_PROMPT_BASE}/workbench/save`,
    body,
  )
  return unwrap(res, '')
}

/** 逻辑删除一条提示词 */
export async function deleteClausePrompt(promptCode: string): Promise<string> {
  const res = await yzhApi.post<ApiResponse<string>>(
    `${CLAUSE_PROMPT_BASE}/workbench/delete`,
    null,
    { params: { code: promptCode } },
  )
  return unwrap(res, '')
}

/** 激活一条提示词（同类型其他提示词自动置为不生效） */
export async function activateClausePrompt(promptCode: string): Promise<string> {
  const res = await yzhApi.post<ApiResponse<string>>(
    `${CLAUSE_PROMPT_BASE}/workbench/activate`,
    null,
    { params: { code: promptCode } },
  )
  return unwrap(res, '')
}

/**
 * AI 生成 / 优化条款属性提示词草稿（**不落库**，返回正文由用户确认后保存）。
 *
 * <p>两种模式：</p>
 * <list type="bullet">
 *   <item><c>currentTemplate</c> = 空 → 按标准<b>从零生成</b></item>
 *   <item><c>currentTemplate</c> 非空 → <b>优化</b>这段现有手写内容</item>
 * </list>
 */
export async function generateClausePrompt(
  body: ClausePromptGenerateBody,
): Promise<ClausePromptGenerateResult> {
  const res = await yzhApi.post<ApiResponse<ClausePromptGenerateResult>>(
    `${CLAUSE_PROMPT_BASE}/workbench/generate`,
    body,
  )
  return unwrap(res, {
    success: false,
    message: '生成失败',
    durationMs: 0,
  })
}

/**
 * 标准下拉（Code = GUID，用于写入 standardCode 字段；Display = 展示名）。
 *
 * <p>⚠️ 返回值用的是 <c>Code</code>（GUID）而不是 <c>StandardCode</c>（可读短码）：
 * <c>wf_prompt_template.StandardCode</c> 存的是 GUID，传短码会静默匹配不到。</p>
 */
export async function getPromptStandards(): Promise<ClausePromptStandard[]> {
  const res = await yzhApi.get<ApiResponse<ClausePromptStandard[]>>(
    `${CLAUSE_PROMPT_BASE}/workbench/standards`,
  )
  return unwrap(res, [])
}
