/**
 * 工作流执行历史 API（阶段四，2026-09-22）
 *
 * 对应后端 `WorkflowTestController` 的只读查询端点：
 *   POST /api/Admin/Workflow/test/history          分页查询执行任务（四层模型第一层）
 *   GET  /api/Admin/Workflow/test/detail/{taskCode} 四层聚合详情（task + item + path + node）
 *
 * 字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。
 */
import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/types'

// ──── 请求契约 ────

export interface TaskHistoryRequest {
  Page?: number
  PageSize?: number
  RuleCode?: string
  TaskType?: string
  TestScope?: string
  TaskStatus?: string
  StartTime?: string
  EndTime?: string
}

// ──── 响应契约 ────

/** 列表行 —— 一条执行任务的摘要 */
export interface TaskHistoryItem {
  TaskCode: string
  TaskType: string
  TestScope: string
  TaskStatus: string
  RuleCode: string
  /** 规则中文名称（执行时快照） */
  RuleName?: string
  /** 违规严重级别 major/minor/observation */
  SeverityIfViolated?: string
  EnterpriseCode: string
  PhaseCode: string
  DurationMs?: number
  StartedAt?: string
  CompletedAt?: string
  ErrorMessage?: string
  CreateTime?: string
  PathCount: number
  NodeCount: number
}

export interface TaskHistoryPage {
  Items: TaskHistoryItem[]
  TotalCount: number
}

/** 第二层：执行项 */
export interface TaskDetailItem {
  ItemCode: string
  RuleCode: string
  ItemType: string
  ItemStatus: string
  IsSuccess?: number
  ErrorMessage?: string
  DurationMs?: number
  StartedAt?: string
  CompletedAt?: string
}

/** 第三层：路径 */
export interface TaskPathDetail {
  PathIndex: number
  Status: string
  ReusedCount: number
  NodeIds: string[]
  FailedAtNodeId?: string
  ErrorMessage?: string
  Output?: any
  DurationMs?: number
  StartedAt?: string
  CompletedAt?: string
}

/** 第四层：节点 */
export interface TaskNodeDetail {
  NodeId: string
  NodeType: string
  /** 用户在设计器中填写的节点名称（专家可见的主标识） */
  NodeTitle: string
  /** 该节点在此工作流中的具体作用描述 */
  NodeDescription?: string
  SkillCode: string
  ExecStatus: string
  Output?: any
  /** 专家审批后最终输出（null=未审批或无修改） */
  ApprovedOutput?: any
  ErrorMessage?: string
  StartedAt?: string
  CompletedAt?: string
  ExecutionTimeMs?: number
  PromptTokens?: number
  CompletionTokens?: number
  LlmDurationMs?: number
  /** LLM 模型名（ai_node 有值） */
  AiModel?: string
  /** 渲染后送 API 的完整 prompt（ai_node 有值） */
  AiPrompt?: string
  /** 源文件编码 cert_extraction_result.FileCode（docField/docTable 有值） */
  SourceFileCode?: string
  /** 源字段中文名（docField 有值） */
  SourceFieldName?: string
  /** 源文件版本号 */
  SourceVersion?: number
  /** 0=新执行 1=复用（去重后 DB 里恒为 0） */
  IsReused: number
}

/** 节点审批详情 */
export interface NodeApprovalDetail {
  NodeId: string
  ApprovalStatus: string  // pending / approved / rejected
  ApprverCode?: string
  ApprverName?: string
  Comment?: string
  Confidence?: number
  ManualResult?: any
  ApprovedAt?: string
}

/** 规则上下文 */
export interface RuleContext {
  RuleCode: string
  RuleName?: string
  RuleNameEn?: string
  ClauseCode?: string
  ClauseNumber?: string
  ClauseTitle?: string
  SeverityIfViolated?: string
  JudgeMode?: string
  NcDescriptionTemplate?: string
}

/** 四层聚合详情 */
export interface TaskExecutionDetail {
  Task: TaskHistoryItem
  Items: TaskDetailItem[]
  Paths: TaskPathDetail[]
  Nodes: TaskNodeDetail[]
  /** 节点审批记录（NodeId → NodeApprovalDetail） */
  NodeApprovals: Record<string, NodeApprovalDetail>
  /** 规则上下文（含条款信息） */
  RuleContext?: RuleContext
}

// ──── API ────

export async function getTaskHistory(
  params: TaskHistoryRequest = {},
): Promise<TaskHistoryPage> {
  const res = await yzhApi.post<ApiResponse<TaskHistoryPage>>(
    '/api/Admin/Workflow/test/history',
    { Page: 1, PageSize: 20, ...params },
  )
  return res.data
}


/** 节点审批请求 */
export interface NodeApprovalRequest {
  TaskCode: string
  NodeId: string
  ApprovalStatus: string  // approved / rejected
  Comment?: string
  Confidence?: number
  ManualResult?: any
}

/** 提交节点审批 */
export async function approveNode(request: NodeApprovalRequest): Promise<void> {
  const res = await yzhApi.post<ApiResponse<{ result: boolean }>>(
    '/api/Admin/Workflow/approve',
    request,
  )
  if (res.success !== true) throw new Error(res.message || res.err || '审批失败')
}

export async function getTaskDetail(taskCode: string): Promise<TaskExecutionDetail> {
  const res = await yzhApi.get<ApiResponse<TaskExecutionDetail>>(
    `/api/Admin/Workflow/test/detail/${encodeURIComponent(taskCode)}`,
  )
  return res.data
}
