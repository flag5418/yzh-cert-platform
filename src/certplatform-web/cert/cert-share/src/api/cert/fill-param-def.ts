/**
 * 体系认证全局参数定义 API（后台管理）
 *
 * ★ 数据模型：`cert_fill_param_def` —— 按「机构 × 标准 × 阶段」预定义全局参数。
 *   `OrgCode` 存的是 **体系认证机构 Code**（`cert_certification_body.Code`），
 *   ⛔ 不是专家工作区 Code（两者是不同的 Code，见 `WorkspaceContextService.ResolveCertBodyCodeAsync` 的对照表）。
 *
 * ★ 三个自定义端点（CRUD 之外）：
 *   - `scopes`           机构 / 标准 / 阶段三个下拉的数据源
 *   - `enterprise-attrs` ★「自动形成带企业所有属性的列表，让用户选择」——
 *                        配 `SourceExpr` 时点选企业属性，而不是手敲字段名
 *   - `effective`        ★ 生效参数集 —— 左树选中「机构 × 标准 × 阶段」后右表的数据
 *
 * ⚠️ 契约：`scopes` / `enterprise-attrs` 的外层是**小写键名**（历史写法）；
 *    而 `effective` 整个响应**一律 PascalCase**。前端按各自实际键名读取，别互相套用。
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse } from '@yzh-core'
import { unwrap } from '@yzh-core'

const BASE = '/api/Cert/FillParamDef'

/** 下拉选项（value = Code，label = 显示名，no = 业务编号） */
export interface ScopeOption {
  value: string
  label: string
  no?: string | null
}

export interface Scopes {
  orgs: ScopeOption[]
  standards: ScopeOption[]
  stages: ScopeOption[]
}

/** 企业属性目录项 —— `expr` 即写入 `cert_fill_param_def.SourceExpr` 的值 */
export interface EnterpriseAttr {
  attr: string
  label: string
  group: string
  valueType: string
  expr: string
  /** 取一台真实企业做的示例值（可能为 null = 该企业此字段为空） */
  sample?: string | null
}

export interface EnterpriseAttrsResult {
  enterpriseCode?: string | null
  enterpriseName?: string | null
  items: EnterpriseAttr[]
}

/** 机构 / 标准 / 阶段下拉数据 */
export async function getScopes(): Promise<Scopes> {
  const res = await yzhApi.get<ApiResponse<Scopes>>(`${BASE}/scopes`)
  return unwrap(res, { orgs: [], standards: [], stages: [] })
}

/** ★ 企业属性目录（带真实示例值） */
export async function getEnterpriseAttrs(enterpriseCode?: string): Promise<EnterpriseAttrsResult> {
  const res = await yzhApi.get<ApiResponse<EnterpriseAttrsResult>>(`${BASE}/enterprise-attrs`, {
    enterpriseCode,
  })
  return unwrap(res, { items: [] })
}

// ════════════════════════════════════════════════════════════════════════
// ★ 生效参数集（左树右表 —— 选中「机构 × 标准 × 阶段」后的右表数据）
// ════════════════════════════════════════════════════════════════════════

/** 作用域类别 → 界面标签（越具体越优先，引擎按 ScopeLevel 取最具体的一条） */
export const SCOPE_KIND_LABEL: Record<string, string> = {
  common: '通用',
  standard: '标准专属',
  stage: '阶段专属',
  standardStage: '标准 × 阶段',
}

/** 作用域类别 → 标签色（越具体颜色越「实」，一眼看出优先级） */
export const SCOPE_KIND_TAG: Record<string, 'info' | 'primary' | 'warning' | 'success'> = {
  common: 'info',
  standard: 'primary',
  stage: 'warning',
  standardStage: 'success',
}

/** 未生效原因 → 界面文字 */
export const SHADOW_REASON_LABEL: Record<string, string> = {
  overridden: '被更具体的同名参数覆写',
  disabled: '已禁用',
}

/**
 * 生效参数行。
 *
 * <para>前 19 个属性与 `cert_fill_param_def` 逐字段对应（PascalCase）；
 * 后面 7 个是**计算列** —— 数据库里没有，由后端 `effective` 端点算出。</para>
 */
export interface EffectiveItem {
  // ──── 定义本体 ────
  Code: string
  OrgCode: string
  StandardCode: string
  StageCode: string
  ParamCode: string
  ParamName: string
  GroupName: string
  ValueType: string
  EnumOptions?: string | null
  SourceKind: string
  SourceExpr?: string | null
  MaintainMode: string
  DefaultValue?: string | null
  Placeholder?: string | null
  IsRequired: boolean
  IsBuiltin: boolean
  SortOrder: number
  Description?: string | null
  IsValid: number

  // ──── ★ 计算列 ────
  /** common | standard | stage | standardStage */
  ScopeKind: string
  /** 作用域可读文字（如「ISO 9001:2015 · 复审」） */
  ScopeText: string
  /** 具体度 0..3：越大越具体（= 后端 ParamValueResolver.Specificity） */
  ScopeLevel: number
  /** 本行是否生效（false = 被覆写 / 已禁用） */
  IsEffective: boolean
  /** 未生效原因：overridden | disabled（生效行为空串） */
  ShadowReason: string
  /** 覆写本行的生效行 Code（仅未生效行有值） */
  ShadowedBy: string
  /** 本行覆写掉的更宽定义的作用域文字（仅生效行有值） */
  ShadowedScopes: string[]
}

export interface EffectiveStats {
  EffectiveCount: number
  ShadowedCount: number
  ByScope: {
    Common: number
    Standard: number
    Stage: number
    StandardStage: number
  }
}

export interface EffectiveResult {
  OrgCode: string
  StandardCode: string
  StageCode: string
  Items: EffectiveItem[]
  Stats: EffectiveStats
}

export interface EffectiveRequest {
  /** 机构 Code（必填） */
  OrgCode: string
  /** 标准 Code（GUID；空 = 只看通用 + 阶段专属） */
  StandardCode?: string
  /** 阶段 Code（GUID；空 = 只看通用 + 标准专属） */
  StageCode?: string
  /** 关键字（按 ParamCode / ParamName 模糊匹配） */
  Keyword?: string
  /** 是否附带「未生效的定义」（被覆写 / 已禁用） */
  IncludeShadowed?: boolean
}

/**
 * ★ 生效参数集 —— 选中一个「机构 × 标准 × 阶段」后，返回引擎**实际会用到**的那批参数。
 *
 * <p>⛔ 不要在前端用 `/filter` + 本地合并替代本接口：
 * 「通用 + 标准专属 + 阶段专属 + 标准×阶段专属」是 OR 组合，且同 `ParamCode`
 * 要按「更具体优先」去重 —— 那是 `ParamValueResolver.PickMostSpecific` 的职责，
 * 前端再实现一份就会出现「后台看到 A 条生效、文档里填的却是 B 条的值」。</p>
 */
export async function getEffectiveParams(req: EffectiveRequest): Promise<EffectiveResult> {
  const res = await yzhApi.post<ApiResponse<EffectiveResult>>(`${BASE}/effective`, req)
  return unwrap(res, {
    OrgCode: req.OrgCode,
    StandardCode: req.StandardCode ?? '',
    StageCode: req.StageCode ?? '',
    Items: [],
    Stats: {
      EffectiveCount: 0,
      ShadowedCount: 0,
      ByScope: { Common: 0, Standard: 0, Stage: 0, StandardStage: 0 },
    },
  })
}
