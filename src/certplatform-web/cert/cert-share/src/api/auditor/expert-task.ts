/**
 * 专家任务系统 API（专家端 · 任务中心）
 *
 * 后端：`CertPlatform.Auditor/Controllers/ExpertTaskController.cs`
 *      （继承 `YzhControllerBase<CertExpertTask>` → `/config` `/filter` `/add` `/update` 等标准端点
 *       + 本文件用到的 10 个自定义端点）
 *
 * ★ 三条契约要点：
 *  1. **列表页走 EntityConfig 驱动**（`SingleTableCore` + `YzhTable`）——列/搜索/按钮都由后端 JSON 决定，
 *     前端只调 `/config` + `/filter`，所以本文件**不提供** list 函数。
 *  2. **工作区隔离在后端**（`OnBuildingFilter` 按 `OrgCode` 等值收敛）——前端**不传任何隔离参数**。
 *  3. **业务失败恒 HTTP 200**，`success` 是唯一判据 → 一律走 `unwrap` / `unwrapOk`（铁律 F-1/F-2），
 *     ⛔ 禁止 `if (!res.success)`。
 *
 * 字段名一律 **PascalCase**（YZH 铁律七：DB 列名 = C# 属性名 = TS 字段名）。
 */

import { unwrap, unwrapOk, yzhApi } from '@yzh-core'
import type { ApiResponse } from '@yzh-core'

const BASE = '/api/Auditor/ExpertTask'

// ══════════════════════════════════════════════════════════════════════
// 一、任务头（= cert_expert_task 行）
// ══════════════════════════════════════════════════════════════════════

/**
 * 任务行。
 *
 * 末尾 6 个字段是**服务端 `OnQueried` 填充的视图字段**（不入库）——
 * 按钮可用性由后端按三层状态推导，前端**不得**自己再算一遍
 * （枚举一变前端就错，这是本项目已记录过的静默陷阱）。
 */
export interface ExpertTaskRow {
  Code: string
  OrgCode: string

  TaskNumber: string
  TaskName: string
  /** NC_CHECK | REPORT_GENERATE */
  TaskType: string
  /** 视图字段：任务类型中文 */
  TaskTypeLabel?: string | null
  /** FULL | PARTIAL */
  ScopeType: string
  /** NEW | REDO | PATCH */
  TaskSource: string

  EnterpriseCode: string
  EnterpriseName?: string | null
  /** ★ cert_cert_stage.Code（GUID），不是业务码 jd01/03 */
  StageCode: string
  StageName?: string | null

  StandardCount: number
  TotalItemCount: number
  AckedCount: number
  ModifiedCount: number
  SkippedCount: number
  PendingCount: number
  FailedCount: number
  GapCount: number
  /** 执行进度 %（队列进度，非认可进度） */
  Progress: number

  /** draft | pending_run | running | completed | failed */
  ExecStatus: string
  /** 视图字段：执行状态中文 */
  ExecStatusLabel?: string | null
  /** 视图字段：是否可提交执行 */
  CanSubmit?: boolean
  /** 视图字段：是否可重试失败项 */
  CanRetry?: boolean
  /** 视图字段：是否可查看结果 */
  CanViewResult?: boolean
  /** 视图字段：是否正占用 D36 业务锁（= 未结束） */
  IsBlocking?: boolean

  /** 视图字段：是否点了「跳过全部补录」 */
  SkipAllGaps: boolean

  SubmitTime?: string | null
  FinishTime?: string | null
  Remark?: string | null
  CreateName?: string | null
  CreateTime: string
}

// ══════════════════════════════════════════════════════════════════════
// 二、向导（第 1 步 锁判定 / 第 3 步 候选 / 第 4 步 创建）
// ══════════════════════════════════════════════════════════════════════

export interface LockCheckRequest {
  EnterpriseCode: string
  StageCode: string
  TaskType: string
  /** 排除自身（重跑/编辑场景） */
  ExcludeTaskCode?: string | null
}

/**
 * 业务锁判定结果。
 *
 * ★ `Blocking*` 字段的意义：让专家**知道是哪条任务挡着**，
 * 而不是只看到一句「已被占用」。界面必须把这几项显示出来。
 */
export interface LockCheckResult {
  CanCreate: boolean
  /** 人话结论，界面直接显示 */
  Message: string
  BlockingTaskCode?: string | null
  BlockingTaskNumber?: string | null
  BlockingTaskName?: string | null
  BlockingExecStatus?: string | null
  BlockingCreateTime?: string | null
  /** 同企业+阶段+类型已存在的任务数（含已结束） */
  ExistingTaskCount: number
  /** 生效检查项数 */
  CandidateCount: number
  /** 历史平均轮次（沿用判定的参考） */
  AvgRoundCount: number
}

export interface TaskCreateRequest {
  TaskName: string
  TaskType: string
  EnterpriseCode: string
  StageCode: string
  /** FULL | PARTIAL */
  ScopeType: string
  /** NEW | REDO | PATCH */
  TaskSource: string
  StandardCodes: string[]
  /** PARTIAL 时勾选的规则/章节 Code；FULL 时忽略 */
  ItemCodes?: string[] | null
  Remark?: string | null
}

export interface TaskCreateResult {
  TaskCode: string
  TaskNumber: string
  TaskName: string
  StandardCount: number
  ItemCount: number
  QueueCount: number
  /**
   * ★ 本次生成的补录清单条数（`R \ H` 差集）
   *
   * ⛔ **不阻断**：清单是信息不是门禁，客户可以忽略。真正的门禁在**执行期** ——
   *   引用的数据为空时，任务项自动失败并提示「缺失必要数据」。
   */
  GapCount: number
  /** 人话摘要，界面直接显示 */
  Summary: string
}

/** 候选检查项（向导第 3 步勾选表的一行） */
export interface TaskCandidate {
  StandardCode: string
  StandardName: string
  /** 规则/章节业务键（`cert_validation_rule.Code` / `cert_report_section.Code`）—— 提交时的 ItemCodes */
  ItemCode: string
  /** 规则编号 / 章节号（展示用） */
  ItemNumber: string
  ItemName: string
  ClauseNumber?: string | null
  ClauseTitle?: string | null
  /** auto | semi | manual（仅 NC） */
  JudgeMode?: string | null
  SeverityDefault?: string | null
  /** ★ 上次检查时间（D30：沿用项保留，界面须显示） */
  LastAuditedTime?: string | null
  LastConclusion?: string | null
  /** 是否已配工作流 DAG（未配 ⇒ 执行时必跳过，界面须预警） */
  HasWorkflow: boolean
  /** 系统建议勾选 */
  Suggested: boolean
}

// ══════════════════════════════════════════════════════════════════════
// 三、详情（4 Tab）
// ══════════════════════════════════════════════════════════════════════

export interface TaskStandard {
  Code: string
  StandardCode: string
  StandardName: string
  ItemCount: number
  DoneCount: number
  FailedCount: number
  SkippedCount: number
  ExecStatus: string
  LifecycleStatus: string
  QueueCode?: string | null
  QueueStatus?: string | null
  Progress: number
}

export interface TaskQueue {
  Code: string
  StandardCode: string
  StandardName: string
  QueueType: string
  /** pending | running | completed | failed | cancelled */
  QueueStatus: string
  TotalCount: number
  DoneCount: number
  FailedCount: number
  SkippedCount: number
  Progress: number
  RetryCount: number
  MaxRetryCount: number
  LockCode?: string | null
  StartTime?: string | null
  FinishTime?: string | null
  LastError?: string | null
  CanStart: boolean
  CanPause: boolean
}

export interface TaskLog {
  Code: string
  LogAction: string
  LogLevel: string
  Message?: string | null
  QueueCode?: string | null
  TaskItemCode?: string | null
  ItemType?: string | null
  ItemName?: string | null
  StandardCode?: string | null
  OperatorName?: string | null
  DurationMs?: number | null
  OperateTime: string
  Payload?: string | null
}

// ── 补录清单（★ 2026-09-30 裁决 J1：两张扁平表，不分文档）────────────────
//
// 变更要点：
//  ① 记录粒度从「字段 × 依赖它的规则」降为「字段/表格」⇒ 1 条 = 1 个待补项
//  ② ⛔ 不按文档分组、不做文档树；`ExpectedFileName` / `StandardFileCode` 只是提示列
//  ③ `ImpactedItems` 由后端**实时反查**工作流 DAG（⛔ 不落库 —— 规则是业务配置、
//     变动频繁，落库必然漂移）

/** 缺口类型：`field` | `table`（`unknown` = 未识别的节点类型，需人工确认） */
export type GapType = 'field' | 'table' | 'unknown'

/** 缺口状态 */
export type GapStatus = 'pending' | 'filled' | 'skipped'

/** 「这条缺口影响哪些检查项」（实时反查，不落库） */
export interface GapImpact {
  ItemCode: string
  ItemName: string
  ItemType: string
  ClauseCode?: string | null
}

export interface TaskGap {
  Code: string
  GapType: GapType
  /** 展示名（字段中文名 / 表格名） */
  GapLabel: string
  /**
   * ★ 规则 Code —— 补录回写的主键成分（裁决 J4：1 文件 = 1 规则）。
   * ⛔ 为空表示工作流节点没配 `ruleCode`，服务端会拒绝补录并给出明确提示。
   */
  RuleCode?: string | null
  FieldCode?: string | null
  TableCode?: string | null
  /** 只读提示：该数据本应来自哪个文件（⚠️ **不作分组键**，J1「不分文档」） */
  ExpectedFileName?: string | null
  StandardFileCode?: string | null
  GapStatus: GapStatus
  StandardCode?: string | null
  ClauseCode?: string | null
  FilledValue?: string | null
  FilledName?: string | null
  FilledTime?: string | null
  SkipName?: string | null
  SkipTime?: string | null
  SkipReason?: string | null
  /**
   * ★ 中文名缺失（文档提取规则页未登记中文名）⇒ 界面提示管理员去补，
   * ⛔ 不是系统故障。实测 `cert_doc_field_def` 可能整表只有 1 行。
   */
  IsUnnamed?: boolean
  /**
   * ★ 表格列定义（中文列头）—— 供「关键表格信息补录」渲染可编辑表格。
   * 字段型恒为空数组。来源 = `cert_doc_table_field_def`。
   */
  Columns?: GapColumn[]
  /** 实时反查：影响几条检查项 */
  ImpactedItemCount: number
  ImpactedItems: GapImpact[]
}

/** ★ 表格列定义（中文列头 + 类型） */
export interface GapColumn {
  /** 列编码（英文驼峰，补录回写用） */
  Code: string
  /** 列中文名（界面列头） */
  Name: string
  /** `string` | `number` | `date` */
  DataType: string
}

/** 清单整体（★ 两张扁平表） */
export interface TaskGapList {
  /** 字段清单 */
  Fields: TaskGap[]
  /** 表格清单 */
  Tables: TaskGap[]
  PendingCount: number
  FilledCount: number
  SkippedCount: number
  /**
   * ★ 待补录项里存在「未命名」项 ⇒ 规则页没配中文名（2026-10-07 裁 3）。
   *
   * 界面须提示管理员去后台「文档提取规则」页补，⛔ 不要让审核员以为系统坏了
   * —— 实测 `cert_doc_field_def` 可能整表只有 1 行。
   */
  HasUnnamedItem?: boolean
}

/** 单条补录结果 */
export interface GapFillOutcome {
  GapCode: string
  GapType: string
  GapLabel: string
  GapStatus: string
  /** 受影响的检查项（前端据此询问"是否立即重跑"） */
  ImpactedItemCodes: string[]
}

/** 批量补录结果 */
export interface GapBatchFillResult {
  FilledGapCount: number
  Items: GapFillOutcome[]
  ImpactedItemCodes: string[]
}

/** 补录留痕一行（`cert_extraction_change_log` · 不可变表，只追加） */
export interface GapChangeLog {
  Code: string
  ResultType: string
  FieldLabel?: string | null
  FieldCode?: string | null
  TableCode?: string | null
  /** `manual_edit` | `archive` | `restore_auto` | `revoke` */
  ChangeAction: string
  OldValue?: string | null
  NewValue?: string | null
  OldValueSource?: string | null
  NewValueSource?: string | null
  OperatorName?: string | null
  OperateTime: string
  Remark?: string | null
}

export interface TaskDetail {
  Task: ExpertTaskRow
  EnterpriseName: string
  StageName: string
  Standards: TaskStandard[]
  Queues: TaskQueue[]
  PendingReviewCount: number
  TotalResultCount: number
}

// ══════════════════════════════════════════════════════════════════════
// 四、端点
// ══════════════════════════════════════════════════════════════════════

/** 业务锁实时判定（向导第 1 步） */
export async function lockCheck(req: LockCheckRequest): Promise<LockCheckResult> {
  const res = await yzhApi.post<ApiResponse<LockCheckResult>>(`${BASE}/lock-check`, req)
  return unwrapOk<LockCheckResult>(res, '锁状态判定失败')
}

/** 候选检查项（向导第 3 步） */
export async function getCandidates(req: TaskCreateRequest): Promise<TaskCandidate[]> {
  const res = await yzhApi.post<ApiResponse<TaskCandidate[]>>(`${BASE}/candidates`, req)
  return unwrap<TaskCandidate[]>(res, [])
}

/** 创建任务（向导第 4 步）—— 事务内落 6 张表 */
export async function createTask(req: TaskCreateRequest): Promise<TaskCreateResult> {
  const res = await yzhApi.post<ApiResponse<TaskCreateResult>>(`${BASE}/create`, req)
  return unwrapOk<TaskCreateResult>(res, '任务创建失败')
}

/** 任务详情（详情页 4 Tab 的一次性载荷） */
export async function getTaskDetail(taskCode: string): Promise<TaskDetail> {
  const res = await yzhApi.post<ApiResponse<TaskDetail>>(`${BASE}/detail`, { TaskCode: taskCode })
  return unwrapOk<TaskDetail>(res, '加载任务详情失败')
}

/** 运行日志（详情 Tab 3） */
export async function getTaskLogs(
  taskCode: string,
  queueCode?: string | null,
  take = 200,
): Promise<TaskLog[]> {
  const res = await yzhApi.post<ApiResponse<TaskLog[]>>(`${BASE}/logs`, {
    TaskCode: taskCode,
    QueueCode: queueCode ?? null,
    Take: take,
  })
  return unwrap<TaskLog[]>(res, [])
}

// ── 提交执行（★ 返回体带软提示，⛔ 不阻断）─────────────────────────────

/**
 * 提交执行结果
 *
 * ★ **不阻断**（裁决 J1）：清单是信息不是门禁，客户可以忽略。
 *   有未补录项时照常入队，只是返回 `Warning` 提示专家。
 *   真正的门禁在执行期 —— 依赖的数据为空时，任务项自动失败 + 提示「缺失必要数据」。
 */
export interface SubmitResult {
  TaskCode: string
  QueueCount: number
  ItemCount: number
  /** 未补录的缺口数（0 = 清单已清空） */
  PendingGapCount: number
  /** 软提示文案（前端直接显示，不要自己拼） */
  Warning?: string | null
  Message: string
}

export async function submitTask(taskCode: string): Promise<SubmitResult> {
  const res = await yzhApi.post<ApiResponse<SubmitResult>>(`${BASE}/submit`, { TaskCode: taskCode })
  return unwrapOk<SubmitResult>(res, '提交执行失败')
}

/** 启动队列 */
export async function startQueue(queueCode: string): Promise<unknown> {
  const res = await yzhApi.post<ApiResponse<unknown>>(`${BASE}/queue/start`, { QueueCode: queueCode })
  return unwrapOk<unknown>(res, '启动队列失败')
}

/** 暂停队列 */
export async function pauseQueue(queueCode: string): Promise<unknown> {
  const res = await yzhApi.post<ApiResponse<unknown>>(`${BASE}/queue/pause`, { QueueCode: queueCode })
  return unwrapOk<unknown>(res, '暂停队列失败')
}

/** 重试失败项（重置为 pending） */
export async function retryFailed(taskCode: string): Promise<unknown> {
  const res = await yzhApi.post<ApiResponse<unknown>>(`${BASE}/retry-failed`, { TaskCode: taskCode })
  return unwrapOk<unknown>(res, '重置失败项失败')
}

// ══════════════════════════════════════════════════════════════════════
// 五、补录清单端点（★ 2026-09-30 · 后端 ExpertTaskDataGapController）
// ══════════════════════════════════════════════════════════════════════

const GAP_BASE = '/api/Auditor/ExpertTaskDataGap'

/**
 * 读补录清单（★ 返回**两张扁平表**：字段 / 表格）
 *
 * ⛔ **不按文档分组**（裁决 J1）。`ExpectedFileName` / `StandardFileCode` 只是提示列。
 */
export async function getGapList(taskCode: string): Promise<TaskGapList> {
  const res = await yzhApi.post<ApiResponse<TaskGapList>>(`${GAP_BASE}/list`, { TaskCode: taskCode })
  // ⛔ 兜底值用 unwrap（第二个参数是 data 兜底），不是 unwrapOk（第二个参数是文案）
  return unwrap<TaskGapList>(res, {
    Fields: [],
    Tables: [],
    PendingCount: 0,
    FilledCount: 0,
    SkippedCount: 0,
  })
}

/**
 * ★★ 启动前预检 —— 「关键信息补录」抽屉的触发点（2026-10-07 用户裁决）
 *
 * 【为什么必须实时重算，而不是读 `list` 的存量行】
 *   缺口清单是**派生数据**（依赖当前企业资料 + 当前规则 DAG），
 *   存量行会随资料/配置变动漂移。本端点在后端复用 `GapDetector.GenerateGapsAsync`
 *   现算后再落库，保证「点启动那一刻」的清单与实际执行口径一致
 *   —— 避免「列表说有缺口、其实早补好了」这类假告警。
 *
 * 【信封语义】
 *   本端点是**查询**（回答「能不能启动、缺什么」）⇒ 查询成功即 `Ok`，
 *   ⛔ 不是 `Fail`。真正的「不启动」由前端按 `PendingCount > 0` 决定是否弹抽屉，
 *   ⛔ 不做硬阻断（用户裁决「运行跳过」）。
 */
export async function precheckGaps(taskCode: string): Promise<TaskGapList> {
  const res = await yzhApi.post<ApiResponse<TaskGapList>>(`${GAP_BASE}/precheck`, {
    TaskCode: taskCode,
  })
  return unwrap<TaskGapList>(res, {
    Fields: [],
    Tables: [],
    PendingCount: 0,
    FilledCount: 0,
    SkippedCount: 0,
    HasUnnamedItem: false,
  })
}

/**
 * 补录单条
 *
 * 落库去向：`cert_extraction_result` / `cert_table_extraction_result`，
 * `ValueSource='manual'`（裁决 J2）。⛔ 不建独立补录表。
 *
 * @param value 字段型 = 字符串；**表格型必须是 JSON 数组字符串**
 *   （★ 2026-10-07 起界面不再让用户手写 JSON —— 「关键信息补录」抽屉用
 *    `gap.Columns`（中文列头）渲染可编辑小表格，提交时由前端 `JSON.stringify`）
 */
export async function fillGap(
  gapCode: string,
  value: string,
  remark?: string | null,
): Promise<GapFillOutcome> {
  const res = await yzhApi.put<ApiResponse<GapFillOutcome>>(`${GAP_BASE}/${gapCode}/fill`, {
    Value: value,
    Remark: remark ?? null,
  })
  return unwrapOk<GapFillOutcome>(res, '补录失败')
}

/** 批量补录（单事务，上限 200；任一条失败整体回滚） */
export async function batchFillGaps(
  items: { GapCode: string; Value: string }[],
  remark?: string | null,
): Promise<GapBatchFillResult> {
  const res = await yzhApi.post<ApiResponse<GapBatchFillResult>>(`${GAP_BASE}/batch-fill`, {
    Items: items,
    Remark: remark ?? null,
  })
  return unwrapOk<GapBatchFillResult>(res, '批量补录失败')
}

/** 跳过单条缺口（客户可以忽略 ⇒ 依赖它的任务项执行时标 `skipped`） */
export async function skipGap(gapCode: string, reason?: string | null): Promise<void> {
  const res = await yzhApi.post<ApiResponse<unknown>>(`${GAP_BASE}/${gapCode}/skip`, {
    Reason: reason ?? null,
  })
  unwrapOk<unknown>(res, '跳过失败')
}

/**
 * 跳过该任务全部待处理缺口
 *
 * ⚠️ 确认文案必须说清后果（08 号 §6.5）：依赖这些数据的检查项会标记为
 * 「数据不足，未检查」，**不计入"符合"数量**。
 */
export async function skipAllGaps(taskCode: string, reason?: string | null): Promise<number> {
  const res = await yzhApi.post<ApiResponse<{ skipped: number }>>(`${GAP_BASE}/skip-all`, {
    TaskCode: taskCode,
    Reason: reason ?? null,
  })
  return unwrap<{ skipped: number }>(res, { skipped: 0 }).skipped
}

/** 补录留痕时间线（`cert_extraction_change_log` · 不可变表，只读） */
export async function getGapChangeLogs(taskCode: string): Promise<GapChangeLog[]> {
  const res = await yzhApi.post<ApiResponse<GapChangeLog[]>>(`${GAP_BASE}/change-logs`, {
    TaskCode: taskCode,
  })
  return unwrap<GapChangeLog[]>(res, [])
}

// ══════════════════════════════════════════════════════════════════════
// 六、未执行清单（2026-10-07 用户裁决 · 裁 2「运行跳过」）
// ══════════════════════════════════════════════════════════════════════

/**
 * 未执行清单一行 —— **按规则/条款聚合**（⛔ 不按缺口逐条罗列）
 *
 * 用户逐字要求：「队列完成后，详细记录，哪些规则或条款未执行成功，什么原因」。
 */
export interface TaskUnexecuted {
  /** 规则 / 章节业务键 */
  ItemCode: string
  /** ★ 规则名 / 章节名（中文） */
  ItemName: string
  /** `nc_check` | `report_section` */
  ItemType: string
  /** 关联条款编码 */
  ClauseCode?: string | null
  /** 条款标题（中文，可空） */
  ClauseTitle?: string | null
  /** 未执行分类（英文枚举，界面用 `SkipCategoryLabel` 显示） */
  SkipCategory: string
  /** ★ 未执行分类的中文名 */
  SkipCategoryLabel: string
  /** ★ 原因（人话，恒非空） */
  Reason: string
  /** ★ 具体缺什么（字段/表格中文名 + 应来自哪个文件） */
  MissingItems: string[]
  StandardCode?: string | null
}

export interface TaskUnexecutedResult {
  Items: TaskUnexecuted[]
  /** 未执行成功的规则/条款条数 */
  TotalCount: number
  /** 其中因「缺企业数据」导致的条数 */
  DataGapCount: number
  /** ★ 队列是否已全部结束（未结束 ⇒ 清单还会变，界面须标注「执行中」） */
  IsQueueFinished: boolean
  /** 任务执行状态 */
  ExecStatus: string
}

/**
 * ★★ 未执行清单 —— 队列完成后，详细记录「哪些规则或条款未执行成功、什么原因」
 *
 * 【为什么必须有这个出口】
 *   `ExpertTaskQueueRunner.RefreshQueueAsync` 在「全部项 skipped」时会把队列置
 *   `completed` —— **报告看起来跑完了，实际有检查项根本没做**，而且完全静默。
 *   本接口就是把这个静默缺口显式记录下来。
 */
export async function getUnexecuted(taskCode: string): Promise<TaskUnexecutedResult> {
  const res = await yzhApi.post<ApiResponse<TaskUnexecutedResult>>(
    '/api/Auditor/ExpertTask/unexecuted',
    { TaskCode: taskCode },
  )
  return unwrap<TaskUnexecutedResult>(res, {
    Items: [],
    TotalCount: 0,
    DataGapCount: 0,
    IsQueueFinished: false,
    ExecStatus: '',
  })
}
