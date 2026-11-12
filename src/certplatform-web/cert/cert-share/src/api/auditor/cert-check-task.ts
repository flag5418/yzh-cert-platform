/**
 * V2 认证检查任务·证据 API（专家端 · 独立认证检查闭环）
 *
 * 后端：
 *   `CertPlatform.Auditor/Controllers/CertTaskController.cs`      → /api/Auditor/CertTask
 *   `CertPlatform.Auditor/Controllers/CertNcController.cs`        → /api/Auditor/CertNc
 *   `CertPlatform.Auditor/Controllers/CertEvidenceController.cs`  → /api/Auditor/CertEvidence
 *
 * 契约：PascalCase 三处一致；业务失败恒 HTTP 200，success 唯一判据。
 */

import { yzhApi } from '@yzh-core'
import type { ApiResponse } from '@yzh-core/types'

const TASK_BASE = '/api/Auditor/CertTask'
const NC_BASE = '/api/Auditor/CertNc'

// ══════════════════════════════════════════════════════════════════════
// 一、任务头（cert_task）
// ══════════════════════════════════════════════════════════════════════

export interface CertTaskRow {
  Code: string
  TaskNo: string
  TaskKind: string // prep | onsite
  PlanCode: string
  EnterpriseCode: string | null
  StageCode: string | null
  AuditType: string | null
  Status: string // pending | running | paused | completed | failed
  ItemTotal: number
  ItemDone: number
  ItemFailed: number
  Progress: number
  PromptCode: string | null
  CreateBy: string | null
  CreateTime: string
  StartedAt: string | null
}

/** V2 认证任务创建请求 */
export interface CertTaskCreateRequest {
  PlanCode: string
  TaskKind: string // prep | onsite
  PromptCode?: string
  RunMode: string // full | incremental | selective
}

export interface CertTaskCreateResponse {
  TaskCode: string
  TaskNo: string
  ItemTotal: number
  Message: string
}

// ══════════════════════════════════════════════════════════════════════
// 二、任务项（cert_task_item）
// ══════════════════════════════════════════════════════════════════════

export interface CertTaskItemRow {
  Code: string
  TaskCode: string
  CheckItemCode: string
  Status: string // waiting | running | done | failed
  EvidenceState: string // none | partial | complete
  AnalysisState: string // none | queued | done | failed
  RunMode: string
  SkipLocked: boolean
  RerunState: string // none | suggested | pending
  ClauseNumbers: string | null
  DeptNames: string | null
  Sort: number
}

// ══════════════════════════════════════════════════════════════════════
// 三、检查结果（cert_check_result）
// ══════════════════════════════════════════════════════════════════════

export interface CertCheckResultRow {
  Code: string
  TaskItemCode: string
  CheckItemCode: string
  PrepState: string | null
  Conclusion: string // pass | ofi | nc | na（落库四态，唯一值域）
  Summary: string | null
  Recommendation: string | null
  Confidence: number
  IsLocked: boolean
  LockedBy: string | null
  LockedAt: string | null
  EvidenceFingerprint: string | null
  AnalysisCode: string | null
}

// ══════════════════════════════════════════════════════════════════════
// 四、NC（cert_nc）
// ══════════════════════════════════════════════════════════════════════

export interface CertNcRow {
  Code: string
  NcNo: string
  CheckItemCode: string
  ClauseCode: string | null
  Severity: string | null
  FactDesc: string | null
  RequirementRef: string | null
  Status: string // pending_review | issued | rectifying | verifying | closed | escalated
  Origin: string | null
  IssuedBy: string | null
  IssuedAt: string | null
  CreatedInTaskCode: string | null
}

export interface CertNcIssueRequest {
  CheckItemCode: string
  FactDesc: string
  RequirementRef?: string
  Severity: string
  CorrectiveReq?: string
  DueDate?: string
  TaskCode?: string
  NcNo?: string
}

// ══════════════════════════════════════════════════════════════════════
// 五、API 函数
// ══════════════════════════════════════════════════════════════════════

/** POST /api/Auditor/CertTask/create — 从计划创建认证任务 */
export async function createCertTask(req: CertTaskCreateRequest): Promise<ApiResponse<CertTaskCreateResponse>> {
  return yzhApi.post(TASK_BASE + '/create', req)
}

/** POST /api/Auditor/CertTask/start — 启动任务 */
export async function startCertTask(taskCode: string): Promise<ApiResponse<{ Success: boolean; Message: string }>> {
  return yzhApi.post(TASK_BASE + '/start', taskCode)
}

/** POST /api/Auditor/CertTask/pause — 暂停任务 */
export async function pauseCertTask(taskCode: string): Promise<ApiResponse<{ Success: boolean; Message: string }>> {
  return yzhApi.post(TASK_BASE + '/pause', taskCode)
}

/** POST /api/Auditor/CertTask/analyze-item — 执行单检查项符合性分析（LLM） */
export interface CertAnalysisResponse {
  AnalysisCode: string
  TaskItemCode: string
  CheckItemCode: string
  Conclusion: string
  LockedSkipped: boolean
  LockedSkippedCount: number
  EvidenceUsedCount: number
  Message: string
  LlmCalled: boolean
  DurationMs: number | null
}

export async function analyzeCheckItem(taskItemCode: string, lockedMode = 'throttle'): Promise<ApiResponse<CertAnalysisResponse>> {
  return yzhApi.post(TASK_BASE + '/analyze-item', { TaskItemCode: taskItemCode, LockedMode: lockedMode })
}

/** POST /api/Auditor/CertTask/lock-result — 锁定结论 */
export async function lockConclusion(resultCode: string, lockReason?: string): Promise<ApiResponse> {
  return yzhApi.post(TASK_BASE + '/lock-result', { ResultCode: resultCode, LockReason: lockReason })
}

/** POST /api/Auditor/CertTask/unlock-result — 解锁结论 */
export async function unlockConclusion(resultCode: string): Promise<ApiResponse> {
  return yzhApi.post(TASK_BASE + '/unlock-result', resultCode)
}

/** POST /api/Auditor/CertTask/save-result — 保存审核员判定（结论 + 不符合程度 + 判定依据） */
export async function saveCheckResult(
  resultCode: string,
  conclusion: string | null,
  severity?: string | null,
  reason?: string,
): Promise<ApiResponse> {
  return yzhApi.post(TASK_BASE + '/save-result', {
    ResultCode: resultCode,
    Conclusion: conclusion,
    Severity: severity ?? undefined,
    Reason: reason,
  })
}

// ── NC 接口 ──

/** POST /api/Auditor/CertNc/issue — 签发 NC */
export async function issueNc(req: CertNcIssueRequest): Promise<ApiResponse<{ NcCode: string; NcNo: string; Status: string; Message: string }>> {
  return yzhApi.post(NC_BASE + '/issue', req)
}

/** POST /api/Auditor/CertNc/transition — NC 状态流转 */
export async function transitionNc(ncCode: string, action: string, remark?: string): Promise<ApiResponse<{ NcCode: string; NcNo: string; Status: string; Message: string }>> {
  return yzhApi.post(NC_BASE + '/transition', { NcCode: ncCode, Action: action, Remark: remark })
}

/** POST /api/Auditor/CertNc/ai-suggest — AI 生成 NC 描述建议 */
export async function aiSuggestNc(taskItemCode: string, checkItemCode: string): Promise<ApiResponse<string>> {
  return yzhApi.post(NC_BASE + '/ai-suggest', { TaskItemCode: taskItemCode, CheckItemCode: checkItemCode })
}

// ══════════════════════════════════════════════════════════════════════
// 六、证据（cert_evidence）
// ══════════════════════════════════════════════════════════════════════

const EVIDENCE_BASE = '/api/Auditor/CertEvidence'

/** 证据行（对齐 cert_evidence 实体字段） */
export interface CertEvidenceRow {
  Code: string
  CheckItemCode: string
  ClauseCode: string | null
  CapturedIn: string
  CapturedTaskCode: string | null
  EvidenceKind: string
  SourceCode: string | null
  SourceName: string | null
  Anchor: string | null
  Snippet: string | null
  LlmSummary: string | null
  ContentMD5: string | null
  PrevContentMD5: string | null
  Status: string
  CapturedAt: string | null
  Sort: number
  CreateBy: string | null
  CreateTime: string
}

export interface CertEvidenceListRequest {
  Keyword?: string
  CheckItemCode?: string
  CapturedIn?: string
  PageIndex: number
  PageSize: number
}

export interface CertEvidenceListResponse {
  Total: number
  Rows: CertEvidenceRow[]
  Message: string
}

export interface CertEvidenceAddRequest {
  SourceName: string
  CheckItemCode?: string
  SourceCode?: string
  EvidenceKind?: string
  CapturedIn?: string
  Snippet?: string
  LlmSummary?: string
  TranscriptText?: string
  Anchor?: string
  AttachmentsJson?: string
}

export interface CertEvidenceAddResponse {
  EvidenceCode: string
  Message: string
}

export interface CertEvidenceUpdateRequest {
  Code: string
  SourceName?: string
  /** 证据类型（interview / onsite_media / sample / manual …） */
  EvidenceKind?: string
  LlmSummary?: string
  Snippet?: string
  SourceCode?: string
  Anchor?: string
  Status?: string
  DroppedReason?: string
}

/** POST /api/Auditor/CertEvidence/list — 分页查询证据列表 */
export async function listEvidence(req: CertEvidenceListRequest): Promise<ApiResponse<CertEvidenceListResponse>> {
  return yzhApi.post(EVIDENCE_BASE + '/list', req)
}

/** POST /api/Auditor/CertEvidence/add — 新增证据 */
export async function addEvidence(req: CertEvidenceAddRequest): Promise<ApiResponse<CertEvidenceAddResponse>> {
  return yzhApi.post(EVIDENCE_BASE + '/add', req)
}

/** POST /api/Auditor/CertEvidence/update — 更新证据 */
export async function updateEvidence(req: CertEvidenceUpdateRequest): Promise<ApiResponse> {
  return yzhApi.post(EVIDENCE_BASE + '/update', req)
}

/** POST /api/Auditor/CertEvidence/delete — 软删证据 */
export async function deleteEvidence(code: string): Promise<ApiResponse> {
  return yzhApi.post(EVIDENCE_BASE + '/delete', { Code: code })
}

// ══════════════════════════════════════════════════════════════════════
// 六·五、认证计划（关闭）
// ══════════════════════════════════════════════════════════════════════

const PLAN_BASE = '/api/Auditor/AuditPlan'

/**
 * POST /api/Auditor/AuditPlan/close — 关闭计划。
 * 关闭后该计划对应的认证准备 / 认证检查一并作废，只能查看。
 */
export async function closeAuditPlan(planCode: string): Promise<ApiResponse<{ Code: string; Status: string }>> {
  return yzhApi.post(PLAN_BASE + '/close', { Code: planCode })
}

// ══════════════════════════════════════════════════════════════════════
// 七、计划统一视图（V2 准备+检查一体化）
// ══════════════════════════════════════════════════════════════════════

/** 部门联系人 */
export interface ContactPerson {
  name: string
  position: string
  phone: string
}

/** 计划检查项主表行 */
export interface CertPlanCheckItemRow {
  DetailCode: string
  ClauseNumbers: string | null
  DeptName: string | null
  DeptCode: string | null
  ContactsJson: string | null
  DeptNames: string | null
  TasksJson: string | null
  PrepState: string | null
  Conclusion: string | null
  /** 系统建议结论 / 建议程度（AI 产出，仅供参考，⛔ 不是最终结论） */
  AiConclusion?: string | null
  AiSeverity?: string | null
  /** 是否已由审核员判定（ConclusionBy 非空） */
  ConclusionConfirmed?: boolean
  Severity: string | null
  NcSuggested: boolean | null
  /** 负责审核员（审核人信息） */
  OwnerCode?: string | null
  OwnerName?: string | null
  IsLocked: boolean
  RawEvidenceCount: number
  CheckEvidenceCount: number
  DoubtCount: number
  SupplementCount: number
  Sort: number
  /** 自动分析状态：none（未分析）/ done（已完成）/ failed（失败） */
  AnalysisState?: string
  /** none / suggested（证据变化后建议重新分析） */
  RerunState?: string
  TaskItemCode?: string | null
  LastAnalyzedAt?: string | null
}

/** 计划统一视图聚合响应 */
export interface CertPlanChecklistResponse {
  PlanCode: string
  PlanName: string | null
  /** 计划状态：active 进行中 / closed 已关闭（关闭后准备/检查只读） */
  PlanStatus?: string | null
  /** 计划已关闭 —— 前端据此把编辑入口全部置灰，只保留查看 */
  PlanClosed?: boolean
  EnterpriseName: string | null
  StageName: string | null
  TasksJson: string | null
  Rows: CertPlanCheckItemRow[]
  /**
   * 该计划当前是否有任务在跑队列 —— 页面锁的唯一真相来源（46 号 §5.5 / F12）。
   * 为 true 时整页只读：队列正在写库，人再写会互相覆盖。
   */
  TaskRunning: boolean
  RunningTaskCode: string | null
  RunningTaskNo: string | null
  /** prep / onsite */
  RunningTaskKind: string | null
  RunningItemTotal: number
  RunningItemDone: number
  RunningItemSkipped: number
  Message: string
}

/** 计划检查项侧面详情 */
export interface CertPlanCheckItemDetail {
  DetailCode: string
  /** 对应 cert_check_result.Code；保存/锁定/解锁的唯一标识，无结论时为 null */
  ResultCode: string | null
  ClauseNumbers: string | null
  DeptNames: string | null
  DeptContactsJson: string | null
  PrepState: string | null
  Conclusion: string | null
  /** 系统建议结论 / 建议程度（AI 产出，仅供参考） */
  AiConclusion?: string | null
  AiSeverity?: string | null
  Severity: string | null
  Reason: string | null
  /** 判定人（Code / 姓名 / 时间） */
  ConclusionBy?: string | null
  ConclusionByName?: string | null
  ConclusionAt?: string | null
  /** 是否已由审核员判定 */
  ConclusionConfirmed?: boolean
  /** 负责审核员（审核人信息） */
  OwnerCode?: string | null
  OwnerName?: string | null
  NcSuggested: boolean | null
  IsLocked: boolean
  LockedBy: string | null
  LockedAt: string | null
  RawEvidences: CertEvidenceRow[]
  CheckEvidences: CertEvidenceRow[]
  DoubtsJson: string | null
  /** 自动分析状态：none（未分析）/ done（已完成）/ failed（失败） */
  AnalysisState?: string
  /** none / suggested（证据变化后建议重新分析） */
  RerunState?: string
  TaskItemCode?: string | null
  AnalyzeCount?: number
  LastAnalyzedAt?: string | null
}

/** 导出报告响应 */
export interface CertExportReportResponse {
  ExportCode: string
  PlanCode: string
  Status: string
  Message: string
}

/** POST /api/Auditor/CertTask/plan-checklist — 按计划查询检查项聚合列表 */
export async function getPlanChecklist(planCode: string): Promise<ApiResponse<CertPlanChecklistResponse>> {
  return yzhApi.post(TASK_BASE + '/plan-checklist', planCode)
}

/** POST /api/Auditor/CertTask/plan-checkitem-detail — 查询单检查项侧面详情 */
export async function getPlanCheckItemDetail(detailCode: string): Promise<ApiResponse<CertPlanCheckItemDetail>> {
  return yzhApi.post(TASK_BASE + '/plan-checkitem-detail', detailCode)
}

/** POST /api/Auditor/CertTask/export-report — 导出计划报告 */
export async function exportPlanReport(planCode: string): Promise<ApiResponse<CertExportReportResponse>> {
  return yzhApi.post(TASK_BASE + '/export-report', planCode)
}

// ══════════════════════════════════════════════════════════════════════
// 八、分支实例列表（认证准备 / 认证检查 两个独立菜单的列表页）
// ══════════════════════════════════════════════════════════════════════

/** 分支实例请求 */
export interface CertStageListRequest {
  StageKind: 'prep' | 'onsite'
  Keyword?: string
  /** all / waiting / running / paused / done / partial / failed */
  Status?: string
  PageIndex?: number
  PageSize?: number
}

/** 分支实例行 —— 一行 = 一个「计划 × 分支」的执行实例 */
export interface CertStageInstanceRow {
  PlanCode: string
  PlanName: string | null
  EnterpriseCode: string | null
  EnterpriseName: string | null
  StageName: string | null
  /** 认证标准名，逗号分隔 */
  Standards: string | null
  StageKind: string
  ItemTotal: number
  ItemDone: number
  ItemLocked: number
  /** 已签发/建议 NC 数（仅 onsite） */
  ItemNc: number
  /** 资料缺失/部分缺失（仅 prep） */
  ItemMissing: number
  ItemDoubt: number
  /** waiting / running / paused / done / partial / failed */
  State: string
  TaskCode: string | null
  TaskNo: string | null
  StartedAt: string | null
  FinishedAt: string | null
  /** 第几轮执行 */
  RunRound: number
  OwnerNames: string | null
  /** 队列正在跑 ⇒ 页面锁 */
  TaskRunning: boolean
}

export interface CertStageListSummary {
  InstanceTotal: number
  Running: number
  Done: number
  Paused: number
  ItemTotal: number
  NcTotal: number
  LockedTotal: number
}

export interface CertStageListResponse {
  Rows: CertStageInstanceRow[]
  Total: number
  PageIndex: number
  PageSize: number
  Summary: CertStageListSummary
  Message: string
}

/** POST /api/Auditor/CertTask/stage-list — 分支实例列表（列表页） */
export async function getStageList(req: CertStageListRequest): Promise<ApiResponse<CertStageListResponse>> {
  return yzhApi.post(TASK_BASE + '/stage-list', req)
}

// ══════════════════════════════════════════════════════════════════════
// 九、跨计划检查项清单（详情页的检查项表）
// ══════════════════════════════════════════════════════════════════════

export interface CertWorklistRequest {
  StageKind: 'prep' | 'onsite'
  Keyword?: string
  PlanCode?: string
  EnterpriseCode?: string
  OwnerCode?: string
  /** all / waiting / analyzed / locked / nc / doubt */
  Status?: string
  PageIndex?: number
  PageSize?: number
}

/** 清单行 —— 一行 = 一个检查项 */
export interface CertWorklistRow {
  PlanCode: string
  PlanName: string | null
  EnterpriseCode: string | null
  EnterpriseName: string | null
  StageName: string | null

  DetailCode: string
  ClauseNumbers: string | null
  /** 条款标题（权威编号 + 标题，如「5.5.1 职责和权限」） */
  ClauseTitles: string | null
  DeptNames: string | null
  DeptCode: string | null
  DeptName: string | null
  ContactsJson: string | null

  /** 负责审核员（准备期可定义，检查期只读） */
  OwnerCode: string | null
  OwnerName: string | null
  /** 该检查项是否已进入认证检查阶段（有 onsite 结论）⇒ 准备期也不可再改审核员 */
  OnsiteStarted: boolean

  StageKind: string
  /** prep：found / partial / missing / unknown / na */
  PrepState: string | null
  /** onsite：pass / ofi / nc / na —— 准备分支恒为 null（不签发 NC） */
  Conclusion: string | null
  /** 系统建议结论 / 建议程度（AI 产出，仅供参考，⛔ 不是最终结论） */
  AiConclusion?: string | null
  AiSeverity?: string | null
  /** 是否已由审核员判定 */
  ConclusionConfirmed?: boolean
  Severity: string | null
  NcSuggested: boolean | null
  ReasonBrief: string | null

  ResultCode: string | null
  AnalysisCode: string | null
  /** 任务项 Code —— analyze-item 的入参；null 表示该分支还没建过任务 */
  TaskItemCode: string | null
  /** waiting / analyzed / locked / failed */
  State: string
  /** 自动分析状态：none（未分析）/ done（已完成）/ failed（失败） */
  AnalysisState?: string
  /** none / suggested（证据变化后建议重新分析） */
  RerunState?: string

  IsLocked: boolean
  LockedBy: string | null
  LockedAt: string | null

  RawEvidenceCount: number
  CheckEvidenceCount: number
  DoubtCount: number
  AnalyzeCount: number
  LastAnalyzedAt: string | null
  Sort: number
}

export interface CertWorklistSummary {
  ItemTotal: number
  Waiting: number
  Analyzed: number
  Locked: number
  /** 自动分析失败 */
  Failed?: number
  /** 已分析但证据变化，建议重新分析 */
  RerunSuggested?: number
  NcCount: number
  MissingCount: number
  DoubtCount: number
  RunningPlans: number
}

export interface CertWorklistPlanOption {
  PlanCode: string
  PlanName: string | null
  EnterpriseName: string | null
  StageName: string | null
  ItemCount: number
}

export interface CertWorklistResponse {
  Rows: CertWorklistRow[]
  Total: number
  PageIndex: number
  PageSize: number
  Summary: CertWorklistSummary
  Plans: CertWorklistPlanOption[]
  /** 计划 Code → 正在跑的任务号 */
  RunningPlanTasks: Record<string, string>
  Message: string
}

/** POST /api/Auditor/CertTask/worklist — 跨计划检查项清单 */
export async function getWorklist(req: CertWorklistRequest): Promise<ApiResponse<CertWorklistResponse>> {
  return yzhApi.post(TASK_BASE + '/worklist', req)
}

/** POST /api/Auditor/CertTask/set-owner — 设置检查项审核员（仅准备阶段可改） */
export async function setCheckItemOwner(
  detailCode: string,
  ownerCode: string | null,
  ownerName: string | null,
): Promise<ApiResponse> {
  return yzhApi.post(TASK_BASE + '/set-owner', {
    DetailCode: detailCode,
    OwnerCode: ownerCode,
    OwnerName: ownerName,
  })
}

// ══════════════════════════════════════════════════════════════════════
// 专家组成员池（设置检查项负责审核员的下拉数据源）
// ══════════════════════════════════════════════════════════════════════

/** 专家组成员选项 */
export interface CertMemberOption {
  Code: string
  Name: string
  /** 职称（组长 / 组员 / 技术专家…） */
  Title?: string | null
  /** 专业 */
  Profession?: string | null
}

/**
 * POST /api/Auditor/CertTask/member-options
 *
 * ★ 审核员必须从专家组成员池里挑（用户 2026-11-01），不能手打姓名 ——
 *   入场计划要按审核员分组派工，手打的姓名在成员池里查不到人就派不出去。
 */
export async function getMemberOptions(
  keyword?: string,
): Promise<ApiResponse<CertMemberOption[]>> {
  return yzhApi.post(TASK_BASE + '/member-options', { Keyword: keyword || undefined })
}

// ══════════════════════════════════════════════════════════════════════
// 条款详细内容（详情页点条款编号的小弹窗）
// ══════════════════════════════════════════════════════════════════════

export interface CertClauseAttrItem {
  /** 属性编码（ClauseIntent / Artifacts / EvidenceChain …） */
  AttrCode: string
  AttrValue?: string | null
}

export interface CertClauseContentRow {
  Code: string
  ClauseNumber: string
  Title: string
  /** 条款原文或摘要 */
  Description?: string | null
  /** ★ 该条款挂接的属性（按 ClauseCode 关联，不是复制正文） */
  Attributes?: CertClauseAttrItem[] | null
  StandardName?: string | null
  Abbrev?: string | null
}

/** GET /api/Auditor/CertQuery/clause-content — 按计划明细取精确条款内容（避免跨标准同号歧义） */
export async function getClauseContent(
  detailCode: string,
): Promise<ApiResponse<CertClauseContentRow[]>> {
  return yzhApi.get('/api/Auditor/CertQuery/clause-content', { detailCode })
}

// ══════════════════════════════════════════════════════════════════════
// 入场检查内容（认证准备「检查内容」：查什么 / 怎么查，可多条）
// ══════════════════════════════════════════════════════════════════════

const ONSITE_PLAN_BASE = '/api/Auditor/CertOnsitePlan'

/** 入场检查内容行 */
export interface CertCheckContentRow {
  Code: string
  CheckItemCode: string
  SeqNo: number
  CheckWhat: string
  CheckMethod: string | null
  TargetDept: string | null
  SampleRequirement: string | null
  ExpectedFinding: string | null
  ClauseCodes: string | null
  /** manual / ai */
  Source: string
  /** planned / done / skipped */
  Status: string
  Sort: number
  Remark: string | null
}

/** 保存检查内容（Code 有值 = 更新） */
export interface CertCheckContentSaveRequest {
  Code?: string
  CheckItemCode: string
  CheckWhat: string
  CheckMethod?: string
  TargetDept?: string
  SampleRequirement?: string
  ExpectedFinding?: string
  ClauseCodes?: string
  Remark?: string
  Sort?: number
}

/** POST /api/Auditor/CertOnsitePlan/list — 查询某检查项的「检查内容」 */
export async function listCheckContents(
  checkItemCode: string,
): Promise<ApiResponse<CertCheckContentRow[]>> {
  return yzhApi.post(ONSITE_PLAN_BASE + '/list', { CheckItemCode: checkItemCode })
}

/** POST /api/Auditor/CertOnsitePlan/save — 新增 / 更新检查内容 */
export async function saveCheckContent(
  req: CertCheckContentSaveRequest,
): Promise<ApiResponse<CertCheckContentRow>> {
  return yzhApi.post(ONSITE_PLAN_BASE + '/save', req)
}

/** POST /api/Auditor/CertOnsitePlan/delete — 删除检查内容（软删） */
export async function deleteCheckContent(code: string): Promise<ApiResponse> {
  return yzhApi.post(ONSITE_PLAN_BASE + '/delete', { Code: code })
}

/** POST /api/Auditor/CertOnsitePlan/generate — 系统自动生成检查内容 */
export async function generateCheckContents(
  checkItemCode: string,
): Promise<ApiResponse<CertCheckContentRow[]>> {
  return yzhApi.post(ONSITE_PLAN_BASE + '/generate', { CheckItemCode: checkItemCode })
}
