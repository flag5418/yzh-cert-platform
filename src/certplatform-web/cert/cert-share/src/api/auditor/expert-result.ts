/**
 * 结果 API（专家端 · NC 检查结果 / 报告结论 两个菜单共用）
 *
 * 后端：`CertPlatform.Auditor/Controllers/ExpertResultController.cs`
 *
 * ★ 职责边界（D31）：任务系统管「建 → 找 → 跑队列」，本模块管「**审批 + 导出**」。
 *   两边数据是同一批结果行，但动作完全不同，所以是两套端点。
 *
 * ★ 左树恒 **3 级**（企业 → 阶段 → 任务），标准不进树（任务在标准之上，1 任务跨 N 标准）。
 * ★ **点企业/阶段节点 → 右表留空**（服务端落实：`TaskCode` 为空直接返回空列表，不发全量查询）。
 *
 * 字段名一律 PascalCase（YZH 铁律七）。
 */

import { unwrap, unwrapOk, yzhApi } from '@yzh-core'
import type { ApiResponse } from '@yzh-core'
import { downloadBlob } from '@share/utils/download'

const BASE = '/api/Auditor/ExpertResult'

/** 结果类型（服务端 `NormalizeType` 认 `report`，其余一律当 NC） */
export type ResultType = 'nc' | 'report'

// ══════════════════════════════════════════════════════════════════════
// 一、左树
// ══════════════════════════════════════════════════════════════════════

/**
 * 结果左树节点（恒 3 级）。
 *
 * `Children` 仅整树模式（`Full: true`）填充 —— 本前端一律用整树模式：
 * 工作区级数据量是「个位数量级」，一次取回比懒加载少 N×M 次往返，
 * 也让「点企业/阶段不发表格请求」这条规则**没有网络竞态**。
 */
export interface ResultTreeNode {
  Code: string
  Name: string
  ParentCode?: string | null
  /** enterprise | stage | task */
  NodeType: string
  IsLeaf: boolean
  TaskType?: string | null
  ExecStatus?: string | null
  PendingCount: number
  TotalCount: number
  /**
   * 子节点（仅整树模式填充）。
   * ⚠️ 服务端懒加载模式下会发 `null`，这里按 `undefined` 声明 ——
   * 消费方一律写 `node.Children ?? []`（`YzhTree` 内部也是这么做的）。
   */
  Children?: ResultTreeNode[]
}

/** 拉取整树（企业 → 阶段 → 任务） */
export async function getResultTree(type: ResultType): Promise<ResultTreeNode[]> {
  const res = await yzhApi.post<ApiResponse<ResultTreeNode[]>>(`${BASE}/tree`, {
    Type: type,
    Full: true,
  })
  return unwrap<ResultTreeNode[]>(res, [])
}

// ══════════════════════════════════════════════════════════════════════
// 二、右表（结论列表）
// ══════════════════════════════════════════════════════════════════════

/** 结论行 */
export interface ResultRow {
  /** 结果行 Code（= 某轮次的结论） */
  Code: string
  /** 实体层行 Code（长期实体，历史下钻用它） */
  ItemCode: string
  RoundNo: number

  ClauseNumber?: string | null
  ClauseTitle?: string | null
  /** 检查项名（NC = 规则名 / 报告 = 章节名） */
  ItemName: string

  StandardCode: string
  StandardName: string

  /** none | ok | ng | skipped | failed | degraded */
  AutoStatus: string
  AutoDescription?: string | null
  AutoSeverity?: string | null
  AutoConfidence?: number | null

  /** conform | nonconform | observation | na（机器永不写） */
  Conformity?: string | null
  /** major | minor | observation */
  Severity?: string | null
  /** 不符合描述（NC） / 章节正文（报告） */
  ContentText?: string | null
  EvidenceRef?: string | null

  /**
   * ★ 2026-09-30：被跳过的分类（`ExpertTaskConst.SkipCategory`）
   * <para><b>关键值</b>：<c>data_gap</c> / <c>data_gap_skipped</c> = <b>数据不足，未检查</b>
   * （引用的字段/表格没有可用值 ⇒ 无法判定，⛔ 不是"不符合"）。</para>
   * <para>其余：<c>no_rule</c>（规则没配 DAG）/ <c>rule_disabled</c> /
   * <c>manual_mode</c>（人工判定项）/ <c>exec_failed</c>（系统或配置错误）。</para>
   */
  SkipCategory?: string | null
  SkipReason?: string | null

  /** ★ 所属任务 Code —— 「去补录」需要它拼路由（`/tasks/:code?tab=gaps`） */
  TaskCode?: string | null

  /** not_started | pending_review | reviewed | modified | skipped | frozen */
  ReviewStatus: string
  /** 结论来源：是否被专家改过 */
  IsModified: boolean
  ReviewName?: string | null
  ReviewTime?: string | null
  ReviewRemark?: string | null

  /** ★ 责任部门（D-E：列先加，数据先空，显示「—」） */
  ResponsibleDept?: string | null
  /** ★ 报告专用：生成方式三档（自动生成 / 引用 NC 结果 / 人工撰写） */
  GenerationMode?: string | null

  CreateTime: string
  ExecutionTaskCode?: string | null

  /** ★ 界面直接可用的结论文本 */
  ConclusionLabel: string
  /** ★ 结论来源标签：自动 / 人工修改 / 人工撰写 / 待人工撰写 */
  SourceLabel: string
}

export interface ResultListRequest {
  Type: ResultType
  /** ⚠️ 为空 ⇒ 服务端返回空列表（「必须选到任务」的服务端落实） */
  TaskCode?: string | null
  StandardCode?: string | null
  ReviewStatus?: string | null
  Page?: number
  PageSize?: number
}

export interface ResultListResponse {
  Rows: ResultRow[]
  Total: number
  Page: number
  PageSize: number
}

export async function getResultList(req: ResultListRequest): Promise<ResultListResponse> {
  const res = await yzhApi.post<ApiResponse<ResultListResponse>>(`${BASE}/list`, req)
  return unwrapOk<ResultListResponse>(res, '加载结论列表失败')
}

// ══════════════════════════════════════════════════════════════════════
// 三、审批（批量认可） / 修改 / 历史
// ══════════════════════════════════════════════════════════════════════

/** 批量认可（勾选多条一次性认可） */
export async function acknowledgeResults(
  type: ResultType,
  codes: string[],
  remark?: string | null,
): Promise<number> {
  const res = await yzhApi.post<ApiResponse<{ Affected: number }>>(`${BASE}/acknowledge`, {
    Type: type,
    Codes: codes,
    Remark: remark ?? null,
  })
  const data = unwrapOk<{ Affected: number }>(res, '认可失败')
  return data?.Affected ?? 0
}

export interface ResultModifyRequest {
  Type: ResultType
  Code: string
  /** NC 专用 */
  Conformity?: string | null
  Severity?: string | null
  EvidenceRef?: string | null
  /** NC 的不符合描述 / 报告的章节正文 */
  ContentText?: string | null
  ReviewRemark?: string | null
}

/**
 * 单行修改。
 * ★ 改完服务端置 `ReviewStatus=modified` + `IsModified=true` ——
 * 导出时「结论来源」列据此显示「人工修改」。
 */
export async function modifyResult(req: ResultModifyRequest): Promise<void> {
  const res = await yzhApi.post<ApiResponse<{ Code: string }>>(`${BASE}/modify`, req)
  unwrapOk<{ Code: string }>(res, '保存修改失败')
}

/** 历史轮次行（同一检查项/章节跑过的每一轮） */
export interface ResultHistoryRow {
  Code: string
  RoundNo: number
  TaskCode?: string | null
  AutoStatus?: string | null
  AutoDescription?: string | null
  AutoContent?: string | null
  AutoSeverity?: string | null
  Conformity?: string | null
  Severity?: string | null
  ContentText?: string | null
  EvidenceRef?: string | null
  ReviewStatus?: string | null
  IsModified?: boolean
  ReviewName?: string | null
  ReviewTime?: string | null
  CreateTime?: string | null
  ExecutionTaskCode?: string | null
}

/** 某检查项/章节的历史轮次（多轮下钻） */
export async function getResultHistory(
  type: ResultType,
  itemCode: string,
): Promise<ResultHistoryRow[]> {
  const res = await yzhApi.post<ApiResponse<ResultHistoryRow[]>>(`${BASE}/history`, {
    Type: type,
    ItemCode: itemCode,
  })
  return unwrap<ResultHistoryRow[]>(res, [])
}

// ══════════════════════════════════════════════════════════════════════
// 四、整体导出（CSV）
// ══════════════════════════════════════════════════════════════════════

/**
 * 导出当前任务的结论列表（CSV，UTF-8 BOM，Excel 可直接打开）。
 *
 * ★ 为什么用 `postBlob` 而不是 `yzhApi.download`：导出接口在业务失败时
 * 仍按平台契约返回 **HTTP 200 + JSON**，`download()` 只判 `res.ok`，
 * 会把错误体当文件存下来（用户拿到一个内容是 `{"success":false...}` 的 .csv）。
 *
 * ★ 文件名由调用方给（后端 `Content-Disposition` 里中文名会退化成
 * ASCII 兜底或空串，解析不可靠；调用方本来就知道任务编号，直接拼更稳）。
 */
export async function exportResults(req: ResultListRequest, fileName: string): Promise<void> {
  const { blob } = await yzhApi.postBlob(`${BASE}/export`, {
    Type: req.Type,
    TaskCode: req.TaskCode ?? null,
    StandardCode: req.StandardCode ?? null,
    ReviewStatus: req.ReviewStatus ?? null,
  })
  downloadBlob(blob, fileName.endsWith('.csv') ? fileName : `${fileName}.csv`)
}
