/**
 * 企业全局参数值 API（专家端 / 企业端）
 *
 * ★ 数据模型：`cert_fill_param_value` —— 只存 **企业人工填写** 的值。
 *   `MaintainMode='auto'` 的参数**不会**落本表（值恒实时取自企业档案），
 *   这是设计不是缺陷（理由见后端 `FillParamValueController` 类注释）。
 *
 * ★ 三个端点：
 *   - `merge-list`  ★ 把「后台定义的全局参数」与「企业已有值」合并成待完善清单
 *   - `save`        批量保存企业完善结果（值来源恒 `manual`，由服务端写死）
 *   - `enterprise-tree` ★ 左树数据源（企业 → 标准，**两级**）
 *
 * ⛔ **没有 `ai-prompt`**（2026-10-07 用户裁决）：本页是简单填写页，信息由人手填，
 *    不由 AI 分析产出 —— 端点与「生成提示词」按钮已删除。
 *
 * ⛔ 本文件**不含**填充预览（`DocumentFill`）客户端 —— 2026-10-06 用户裁决：
 *    预览 UI 归企业资料规范化册承载，后端 `DocumentFillController` 端点与引擎保留。
 *
 * ⚠️ 契约：载荷 **PascalCase**（与 C# 属性、DB 列名一致），⛔ 不要按 camelCase 读；
 *    空值属性会被序列化器**省略**（不是 null），前端一律用 `?? ''` 兜底。
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse } from '@yzh-core'
import { unwrap } from '@yzh-core'

const BASE = '/api/Auditor/FillParamValue'

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
  /**
   * 取值来源类别 —— 迁移后恒 `manual`（`20261007_fill_param_drop_ai_V1.sql`）。
   * ⛔ 原 `ai` 取值已作废：专家端不再有「生成提示词」按钮。
   */
  SourceKind: string
  SourceExpr?: string | null
  IsRequired: boolean
  IsBuiltin: boolean
  SortOrder: number
  Description?: string | null
  Placeholder?: string | null

  /** 当前值（可能被序列化器省略 ⇒ 用 `?? ''` 兜底） */
  ParamValue?: string | null
  /** auto | manual | default | empty */
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
  /**
   * ⛔ 专家端**不再传**（2026-10-07 起）：树只到标准一级，后台定义 `StageCode` 恒空串。
   * 字段保留只为向后兼容 —— 省略时后端按空串匹配（= 不限阶段，即全部定义）。
   */
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
  // ⛔ 刻意没有 `ValueSource`：值来源恒 `manual`，由服务端写死
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

// ════════════════════════════════════════════════════════════════
// ★ 企业树（企业 → 标准，两级）—— 左树数据源
// ════════════════════════════════════════════════════════════════

/**
 * 企业树节点。
 *
 * <p>⛔ 不是内核 `TreeNode`：后端只出业务字段，前端补 `IsLeaf` / `Extra.Icon`
 * 等渲染所需信息（见 `logic.ts` 的 `toTreeNodes`）。</p>
 *
 * <p>★ **没有阶段字段**（2026-10-07 用户裁决）：树只到标准一级，
 * 后台「企业资料参数」的定义也只按标准组织（`StageCode` 恒空串）。</p>
 */
export interface EnterpriseTreeNode {
  /** 节点唯一键：`ent:{企业}` / `std:{企业}|{标准}` */
  Code: string
  Name: string
  /** enterprise | standard */
  NodeType: string
  EnterpriseCode: string
  /** 标准 Code（GUID = cert_iso_standard.Code） */
  StandardCode: string
  /** 副标题（企业编号 / 标准名） */
  Subtitle: string
  Children: EnterpriseTreeNode[]
}

export interface EnterpriseTreeResult {
  Nodes: EnterpriseTreeNode[]
  EnterpriseCount: number
  LinkCount: number
  /** 空树时的兜底提示（如「企业还没有关联标准，请先到『阶段标准关联』配置」） */
  Hint: string
}

/**
 * ★ 企业树 —— 左树数据源（企业 → 标准，**两级**）。
 *
 * <p>选中的层级决定 `merge-list` 的作用域语义：</p>
 * <ul>
 *   <li>点<b>企业</b>节点 → 标准留空 → 只看「通用参数」</li>
 *   <li>点<b>标准</b>节点 → 「通用 + 该标准专属」</li>
 * </ul>
 * <p>两种点击都有明确语义，不会出现「点了没反应」的死节点。</p>
 * <p>⛔ **没有阶段层**：挂一层阶段只会让同一份清单重复出现 N 次。</p>
 */
export async function getEnterpriseTree(): Promise<EnterpriseTreeResult> {
  const res = await yzhApi.post<ApiResponse<EnterpriseTreeResult>>(`${BASE}/enterprise-tree`, {})
  return unwrap(res, { Nodes: [], EnterpriseCount: 0, LinkCount: 0, Hint: '' })
}
