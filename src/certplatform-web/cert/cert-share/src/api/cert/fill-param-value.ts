/**
 * 企业全局参数值 API（专家端 / 企业端）
 *
 * ★ 数据模型：`cert_fill_param_value` —— 只存 **企业人工填写** 的值。
 *   `MaintainMode='auto'` 的参数**不会**落本表（值恒实时取自企业档案），
 *   这是设计不是缺陷（理由见后端 `FillParamValueController` 类注释）。
 *
 * ★ 三个端点：
 *   - `merge-list`  ★ 把「后台定义的全局参数」与「企业已有基本信息」合并成待完善清单
 *   - `save`        批量保存企业完善结果（只落 manual / both 两类）
 *   - `ai-prompt`   ★ 为 `SourceKind='ai'` 的参数产出可投喂模型的提示词
 *
 * ★ 另一个控制器 `DocumentFillController` 提供 4 项能力的演示与填充预览。
 *
 * ⚠️ 契约：载荷 **PascalCase**（与 C# 属性、DB 列名一致），⛔ 不要按 camelCase 读；
 *    空值属性会被序列化器**省略**（不是 null），前端一律用 `?? ''` 兜底。
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse } from '@yzh-core'
import { unwrap } from '@yzh-core'

const BASE = '/api/Auditor/FillParamValue'
const FILL_BASE = '/api/Auditor/DocumentFill'

export interface ScopeOption {
  value: string
  label: string
  no?: string | null
}

export interface FillScopes {
  standards: ScopeOption[]
  stages: ScopeOption[]
}

export interface EnterpriseOption {
  value: string
  label: string
  no?: string | null
}

/** 合并后的参数条目（PascalCase，与后端 `MergedItem` 逐字对应） */
export interface MergedParamItem {
  ParamCode: string
  ParamName: string
  GroupName: string
  ValueType: string
  EnumOptions?: string | null
  /** auto | manual | both —— 决定 `Editable` 与「能否被自动覆盖」 */
  MaintainMode: string
  /** global | replace | headerFooter | ai */
  SourceKind: string
  SourceExpr?: string | null
  IsRequired: boolean
  IsBuiltin: boolean
  SortOrder: number
  Description?: string | null
  Placeholder?: string | null

  /** 当前值（可能被序列化器省略 ⇒ 用 `?? ''` 兜底） */
  ParamValue?: string | null
  /** auto | manual | ai | default | empty */
  ValueSource: string
  /** 值来源说明（如「企业基础信息 · 企业全称」） */
  SourceRef: string
  IsManualEdited: boolean
  IsFilled: boolean
  /** 企业端能否编辑；false = 只读，应引导去「企业管理」修改 */
  Editable: boolean
}

export interface MergedGroup {
  GroupName: string
  Items: MergedParamItem[]
}

export interface FillProgress {
  Total: number
  Filled: number
  Empty: number
  /** 0~1 的**比值**（不是百分数），前端负责 ×100 */
  Completion: number
  Required: number
  RequiredFilled: number
}

/**
 * `merge-list` 的响应。
 *
 * <p>★ 全字段 PascalCase —— 后端曾用 camelCase，前端按 `enterprise.Name` 读恒得 undefined
 * ⇒「企业名永远显示为空且不报错」。现已统一。</p>
 */
export interface MergeListResult {
  /** 参数定义归属 = **体系认证机构** Code（⛔ 不是工作区 Code） */
  OrgCode: string
  /** 企业归属 = **工作区** Code */
  WorkspaceCode: string
  StandardCode: string
  StageCode: string
  /** 企业档案快照（`{ Code, Name, CreditCode, ... }`，供顶部展示） */
  Enterprise: Record<string, any>
  Groups: MergedGroup[]
  Progress: FillProgress
}

export interface MergeListRequest {
  EnterpriseCode: string
  StandardCode?: string
  StageCode?: string
}

/** 标准 / 阶段下拉（企业端不提供机构下拉 —— 机构 = 当前工作区，由服务端解析） */
export async function getFillScopes(): Promise<FillScopes> {
  const res = await yzhApi.get<ApiResponse<FillScopes>>(`${BASE}/scopes`)
  return unwrap(res, { standards: [], stages: [] })
}

/** 本工作区企业下拉 */
export async function getEnterprises(): Promise<EnterpriseOption[]> {
  const res = await yzhApi.get<ApiResponse<EnterpriseOption[]>>(`${BASE}/enterprises`)
  return unwrap(res, [])
}

/** ★ 合并待完善清单 */
export async function getMergeList(req: MergeListRequest): Promise<MergeListResult> {
  const res = await yzhApi.post<ApiResponse<MergeListResult>>(`${BASE}/merge-list`, req)
  return unwrap(res, {
    OrgCode: '', WorkspaceCode: '', StandardCode: '', StageCode: '',
    Enterprise: {}, Groups: [],
    Progress: { Total: 0, Filled: 0, Empty: 0, Completion: 0, Required: 0, RequiredFilled: 0 },
  })
}

export interface SaveParamItem {
  ParamCode: string
  ParamValue: string
  /** manual（默认）| ai */
  ValueSource?: string
}

export interface SaveResult {
  savedCount: number
  ignoredAuto: string[]
  message: string
}

/** 批量保存企业完善结果 */
export async function saveValues(
  req: MergeListRequest & { Items: SaveParamItem[] },
): Promise<SaveResult> {
  const res = await yzhApi.post<ApiResponse<SaveResult>>(`${BASE}/save`, req)
  return unwrap(res, { savedCount: 0, ignoredAuto: [], message: '' })
}

export interface AiPromptResult {
  paramCode: string
  paramName: string
  valueType: string
  prompt: string
  contextCount: number
  note: string
}

/** ★ 为 AI 参数产出提示词（本期不直连模型） */
export async function getAiPrompt(
  enterpriseCode: string,
  paramCode: string,
): Promise<AiPromptResult> {
  const res = await yzhApi.post<ApiResponse<AiPromptResult>>(`${BASE}/ai-prompt`, {
    EnterpriseCode: enterpriseCode,
    ParamCode: paramCode,
  })
  return unwrap(res, {
    paramCode, paramName: paramCode, valueType: 'text', prompt: '', contextCount: 0, note: '',
  })
}

// ════════════════════════════════════════════════════════════════════════
// ★ 企业树（企业 → 标准 → 阶段）—— 左树数据源
// ════════════════════════════════════════════════════════════════════════

/**
 * 企业树节点。
 *
 * <p>⛔ 不是内核 `TreeNode`：后端只出业务字段，前端补 `IsLeaf` / `Extra.Icon`
 * 等渲染所需信息（见 `logic.ts` 的 `toTreeNodes`）。</p>
 */
export interface EnterpriseTreeNode {
  /** 节点唯一键：`ent:{企业}` / `std:{企业}|{标准}` / `stage:{企业}|{标准}|{阶段}` */
  Code: string
  Name: string
  /** enterprise | standard | stage */
  NodeType: string
  EnterpriseCode: string
  /** 标准 Code（GUID = cert_iso_standard.Code） */
  StandardCode: string
  /** 阶段 Code（GUID = cert_cert_stage.Code） */
  StageCode: string
  /** 副标题（企业编号 / 标准名 / 阶段业务码） */
  Subtitle: string
  /** 后代阶段数 */
  StageCount: number
  Children: EnterpriseTreeNode[]
}

export interface EnterpriseTreeResult {
  Nodes: EnterpriseTreeNode[]
  EnterpriseCount: number
  LinkCount: number
  /** 空树时的兜底提示（如「企业还没有关联标准 / 阶段，请先到『阶段标准关联』配置」） */
  Hint: string
}

/**
 * ★ 企业树 —— 左树数据源（企业 → 标准 → 阶段）。
 *
 * <p>选中的层级决定 `merge-list` 的作用域语义：</p>
 * <ul>
 *   <li>点<b>企业</b>节点 → 标准 / 阶段都留空 → 只看「通用参数」</li>
 *   <li>点<b>标准</b>节点 → 阶段留空 → 「通用 + 该标准专属」</li>
 *   <li>点<b>阶段</b>节点 → 「通用 + 该标准专属 + 该阶段专属 + 该标准×该阶段专属」</li>
 * </ul>
 * <p>三种点击都有明确语义，不会出现「点了没反应」的死节点。</p>
 */
export async function getEnterpriseTree(): Promise<EnterpriseTreeResult> {
  const res = await yzhApi.post<ApiResponse<EnterpriseTreeResult>>(`${BASE}/enterprise-tree`, {})
  return unwrap(res, { Nodes: [], EnterpriseCount: 0, LinkCount: 0, Hint: '' })
}

// ════════════════════════════════════════════════════════════════════════
// 文档填充（4 项能力的演示 + 预览）
// ════════════════════════════════════════════════════════════════════════

export interface FillCapability {
  kind: string
  name: string
  order: number
  syntax: string
  meaning: string
  example: string
}

export interface CapabilitiesResult {
  capabilities: FillCapability[]
  demoTemplate: string
  demoHeader: string
  demoFooter: string
  anchorPattern: string
}

export interface FillHit {
  token: string
  key: string
  kind: string
  kindName: string
  value: string
  source: string
}

export interface FillPending {
  token: string
  key: string
  kind: string
  kindName: string
  reason: string
}

export interface FillReport {
  total: number
  resolved: number
  pending: number
  completion: number
  byKind: { kind: string; name: string; resolved: number; pending: number }[]
  hits: FillHit[]
  pendings: FillPending[]
}

export interface PreviewResult {
  enterpriseCode: string
  enterpriseName: string
  standardCode: string
  stageCode: string
  output: string
  header?: string | null
  footer?: string | null
  report: FillReport
  stats: {
    paramCount: number
    autoMappedCount: number
    filledFromTableCount: number
    definedCount: number
    undefinedTokenCount: number
  }
  summary: string
}

export interface PreviewRequest {
  EnterpriseCode: string
  StandardCode?: string
  StageCode?: string
  /** 留空 = 用内置演示模板 */
  Template?: string
  Header?: string
  Footer?: string
  AiEnabled?: boolean
}

/** 填充能力清单 + 锚点语法 + 内置演示模板 */
export async function getCapabilities(): Promise<CapabilitiesResult> {
  const res = await yzhApi.get<ApiResponse<CapabilitiesResult>>(`${FILL_BASE}/capabilities`)
  return unwrap(res, {
    capabilities: [], demoTemplate: '', demoHeader: '', demoFooter: '', anchorPattern: '',
  })
}

/** ★ 填充预览：企业 + 模板 → 成文 + 证据报告 */
export async function getPreview(req: PreviewRequest): Promise<PreviewResult> {
  const res = await yzhApi.post<ApiResponse<PreviewResult>>(`${FILL_BASE}/preview`, req)
  return unwrap(res, {
    enterpriseCode: '', enterpriseName: '', standardCode: '', stageCode: '',
    output: '', report: { total: 0, resolved: 0, pending: 0, completion: 0, byKind: [], hits: [], pendings: [] },
    stats: { paramCount: 0, autoMappedCount: 0, filledFromTableCount: 0, definedCount: 0, undefinedTokenCount: 0 },
    summary: '',
  })
}
