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
  Conclusion: string // conformant | minor_nc | major_nc | observation | unknown
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
  Severity: string | null
  NcSuggested: boolean | null
  RawEvidenceCount: number
  CheckEvidenceCount: number
  DoubtCount: number
  SupplementCount: number
  Sort: number
}

/** 计划统一视图聚合响应 */
export interface CertPlanChecklistResponse {
  PlanCode: string
  PlanName: string | null
  EnterpriseName: string | null
  StageName: string | null
  TasksJson: string | null
  Rows: CertPlanCheckItemRow[]
  Message: string
}

/** 计划检查项侧面详情 */
export interface CertPlanCheckItemDetail {
  DetailCode: string
  ClauseNumbers: string | null
  DeptNames: string | null
  DeptContactsJson: string | null
  PrepState: string | null
  Conclusion: string | null
  Severity: string | null
  Reason: string | null
  NcSuggested: boolean | null
  IsLocked: boolean
  LockedBy: string | null
  LockedAt: string | null
  RawEvidences: CertEvidenceRow[]
  CheckEvidences: CertEvidenceRow[]
  DoubtsJson: string | null
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
