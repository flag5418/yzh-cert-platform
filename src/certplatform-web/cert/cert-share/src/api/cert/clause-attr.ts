/**
 * 条款属性（属性池 + 条款配置）API
 *
 * ★ 后端：
 *   - 属性池 L0：`/api/Admin/Cert/ClauseAttrDef`（`YzhControllerBase<ClauseAttrDef>`）
 *   - 条款配置 L1：`/api/Admin/Cert/ClauseAttr`（`YzhControllerBase<ClauseAttr>`）
 *
 * ★ CRUD 全走内核约定端点（`/config` `/filter` `/add` `/update` `/delete` `/toggle-valid`），
 *   本文件只放**两个非内核端点**：
 *   1. 左树「标准 → 叶子条款」—— 树数据源是 `cert_iso_clause`，不是本实体
 *   2. 属性下拉选项 —— 给配置页的「属性」选择器
 *
 * ⛔ 设计约束（`docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md`）：
 *   - 属性池 Code 是人可读驼峰（`Artifacts`），不是 GUID
 *   - `ValueSource` 只在条款配置上有，只 `fixed` / `reference` 两档
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse, TreeItemDto } from '@yzh-core'
import { unwrap } from '@yzh-core'

/** 属性池控制器路由 */
export const CLAUSE_ATTR_DEF_BASE = '/api/Admin/Cert/ClauseAttrDef'

/** 条款属性配置控制器路由 */
export const CLAUSE_ATTR_BASE = '/api/Admin/Cert/ClauseAttr'

/** 属性下拉选项（只含启用的属性） */
export interface ClauseAttrOption {
  Code: string
  AttrName: string
  ValueType: string
  DictCode?: string | null
  Remark?: string | null
}

/**
 * 条款属性行（`cert_clause_attr` 一行 = 某条款上的某个属性）。
 *
 * <p>⚠️ `UpdateCore` 会覆盖所有可写列 ⇒ 更新时必须整行回传（含三键），
 * 只传 `Code + AttrValue` 会被后端判定为「三键不可修改」。</p>
 */
export interface ClauseAttrRow {
  Code: string
  StandardCode: string
  ClauseCode: string
  AttrCode: string
  AttrValue?: string | null
  ValueSource?: 'fixed' | 'reference' | string
  Sort?: number
  IsValid?: number
  [key: string]: any
}

/** 字典项（`GET /api/System/Dictionary/items/by-no/{dicNo}`，PascalCase） */
export interface DictItemDto {
  Value: string
  Label: string
  Code?: string
  Color?: string
}

/**
 * 左树：标准 → 章 → 节 → 叶子条款（完整层次，最多 4 层）。
 */
export async function getClauseAttrTree(): Promise<TreeItemDto[]> {
  const res = await yzhApi.post<ApiResponse<TreeItemDto[]>>(
    `${CLAUSE_ATTR_BASE}/tree/root`,
    {},
  )
  return unwrap(res, [])
}

/** 属性池下拉选项 */
export async function getClauseAttrOptions(): Promise<ClauseAttrOption[]> {
  const res = await yzhApi.get<ApiResponse<ClauseAttrOption[]>>(
    `${CLAUSE_ATTR_DEF_BASE}/options`,
  )
  return unwrap(res, [])
}

/** 每个条款已配属性数（给树节点打角标，可选） */
export async function getClauseAttrCounts(): Promise<Record<string, number>> {
  const res = await yzhApi.post<ApiResponse<Record<string, number>>>(
    `${CLAUSE_ATTR_BASE}/tree/counts`,
    {},
  )
  return unwrap(res, {})
}

/**
 * 某条款已配置的属性行（**含已停用**，否则停用行会被当成"没配"重复新增）。
 *
 * <p>走内核 `/filter`：`ShowDisabled=true` + 条款/标准双条件（标准条件是索引成员，
 * 条款 Code 已全局唯一，带上只为走覆盖索引）。</p>
 */
export async function listClauseAttrs(params: {
  ClauseCode: string
  StandardCode: string
}): Promise<ClauseAttrRow[]> {
  const res = await yzhApi.post<
    ApiResponse<{ Items?: ClauseAttrRow[]; TotalCount?: number }>
  >(`${CLAUSE_ATTR_BASE}/filter`, {
    Page: 1,
    PageSize: 200,
    ShowDisabled: true,
    SortField: 'Sort',
    SortOrder: 'asc',
    Filters: [
      { Field: 'ClauseCode', Operator: 'eq', Value: params.ClauseCode },
      { Field: 'StandardCode', Operator: 'eq', Value: params.StandardCode },
    ],
  })
  const page = unwrap(res, { Items: [], TotalCount: 0 })
  return page.Items ?? []
}

/** 新增条款属性行（返回落库后的整行） */
export async function createClauseAttr(entity: Partial<ClauseAttrRow>): Promise<ClauseAttrRow> {
  const res = await yzhApi.post<ApiResponse<ClauseAttrRow>>(`${CLAUSE_ATTR_BASE}/add`, entity)
  return unwrap(res, {} as ClauseAttrRow)
}

/** 更新条款属性行（★ 必须整行回传：三键 + AttrValue + ValueSource + IsValid） */
export async function updateClauseAttr(entity: Partial<ClauseAttrRow>): Promise<ClauseAttrRow> {
  const res = await yzhApi.post<ApiResponse<ClauseAttrRow>>(`${CLAUSE_ATTR_BASE}/update`, entity)
  return unwrap(res, {} as ClauseAttrRow)
}

/** 批量删除条款属性行（软删除，Code 数组） */
export async function deleteClauseAttrs(codes: string[]): Promise<void> {
  const res = await yzhApi.post<ApiResponse<string>>(`${CLAUSE_ATTR_BASE}/delete`, codes)
  unwrap(res, '')
}

/** 字典项（`dict` / `json` 类型属性的可选值） */
export async function getDictItems(dicNo: string): Promise<DictItemDto[]> {
  const res = await yzhApi.get<ApiResponse<DictItemDto[]>>(
    `/api/System/Dictionary/items/by-no/${encodeURIComponent(dicNo)}`,
  )
  return unwrap(res, [])
}

// ============================================================
// 提示词维护（sys_config · ConfigKey = clause_attr_prompt）
// ============================================================

/** 提示词读取结果（编辑抽屉用，PascalCase = C# DTO） */
export interface ClauseAttrPromptDto {
  ConfigKey: string
  DisplayName: string
  /** 提示词正文（含 {{attr_pool}} 占位符） */
  Template: string
  /** 当前属性池渲染出的注入段（只读预览） */
  AttrPoolSection: string
  /** 属性池启用条数 */
  AttrCount: number
  /** 库里是否已有该参数行（false = 用的是兜底正文） */
  Stored: boolean
  UpdateBy?: string
  UpdateTime?: string
}

/** 可用占位符（前端硬编码 —— 与 ClauseAttrPromptService 的渲染契约对齐） */
export const CLAUSE_ATTR_PROMPT_PLACEHOLDERS = [
  { token: '{{standard_name}}', desc: '标准名 + 可读短码（如「ISO 9001（iso9001）」）' },
  { token: '{{clause_number}}', desc: '条款号（如 7.1.5.2）' },
  { token: '{{clause_title}}', desc: '条款标题' },
  { token: '{{clause_content}}', desc: '条款正文全文' },
  { token: '{{attr_pool}}', desc: '★ 属性池注入位（删除后会自动追加到末尾，不建议删）' },
]

/** 读取提示词（属性池注入段实时渲染） */
export async function getClausePrompt(): Promise<ClauseAttrPromptDto> {
  const res = await yzhApi.get<ApiResponse<ClauseAttrPromptDto>>(
    `${CLAUSE_ATTR_BASE}/prompt`,
  )
  return unwrap(res, {
    ConfigKey: 'clause_attr_prompt',
    DisplayName: '条款属性预生成提示词',
    Template: '',
    AttrPoolSection: '',
    AttrCount: 0,
    Stored: false,
  })
}

/** 保存提示词（幂等 upsert） */
export async function saveClausePrompt(template: string): Promise<void> {
  const res = await yzhApi.post<ApiResponse<string>>(
    `${CLAUSE_ATTR_BASE}/prompt`,
    { Template: template },
  )
  unwrap(res, '')
}

/** AI 预生成入参 */
export interface ClauseAttrGenerateParams {
  /** 标准短码（如 iso9001） */
  StandardCode: string
  /** 指定条款；为空 = 该标准全部叶子条款 */
  ClauseCodes?: string[]
  /** 已存在的属性是否覆盖（默认 false = 只补缺失） */
  Overwrite?: boolean
  /** 单次最多跑几条（默认 5，上限 50 —— 服务端硬限） */
  MaxCount?: number
}

/** 单条条款的生成结果 */
export interface ClauseAttrGenerateItem {
  ClauseCode: string
  ClauseNumber: string
  Title: string
  Success: boolean
  Message: string
  Written: number
  Skipped: number
  PromptTokens: number
  CompletionTokens: number
  RawOutput: string
}

/** 整体生成结果 */
export interface ClauseAttrGenerateResult {
  Success: boolean
  Message: string
  Total: number
  Succeeded: number
  Failed: number
  Written: number
  Skipped: number
  PromptCode: string
  /**
   * 本次用的提示词来源（★ 可见性保证）。
   *
   * <p>`stored` = 库里已保存的版本（抽屉里改的就是它）；
   * `fallback` = 代码内兜底正文 —— 说明抽屉里的正文<b>没生效</b>，别去怪模型。</p>
   */
  PromptScope: 'stored' | 'fallback' | ''
  Model: string
  Items: ClauseAttrGenerateItem[]
}

/**
 * AI 预生成条款属性（★ 服务端**串行**跑，单条数秒 ⇒ 前端必须给"正在跑"的明确反馈）。
 *
 * <p>⚠️ 信封语义：前置校验没过（没选标准 / 提示词没配 / AI 参数没填）服务端返回
 * `success:false`（业务失败）⇒ 这里用 `unwrap` 会抛错，交给页面统一报错；
 * 真跑过了返回 `success:true`，逐条成败看 `data.Success` / `data.Items`。</p>
 */
export async function generateClauseAttrs(
  params: ClauseAttrGenerateParams,
): Promise<ClauseAttrGenerateResult> {
  const res = await yzhApi.post<ApiResponse<ClauseAttrGenerateResult>>(
    `${CLAUSE_ATTR_BASE}/generate`,
    params,
  )
  return unwrap(res, {
    Success: false,
    Message: '',
    Total: 0,
    Succeeded: 0,
    Failed: 0,
    Written: 0,
    Skipped: 0,
    PromptCode: '',
    PromptScope: '',
    Model: '',
    Items: [],
  })
}
