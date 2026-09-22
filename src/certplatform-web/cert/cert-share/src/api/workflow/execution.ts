/**
 * 工作流执行历史 API（阶段四，2026-09-22）
 *
 * 对应后端 `WorkflowTestController` 的只读查询端点：
 *   POST /api/Workflow/test/history          分页查询执行任务（四层模型第一层）
 *   GET  /api/Workflow/test/detail/{taskCode} 四层聚合详情（task + item + path + node）
 *
 * 字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。
 * 例外：`NcResult` / 输出字典内层键是运行时动态载荷（camelCase，已登记例外 E3），此处不涉及。
 */
import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/types'

// ──── 请求契约 ────

/** 测试历史查询条件（全部可选，彼此 AND） */
export interface TaskHistoryRequest {
  Page?: number
  PageSize?: number
  /** 规则编码精确匹配 */
  RuleCode?: string
  /** TEST | NC_CHECK | REPORT_GENERATE */
  TaskType?: string
  /** FULL | NODE | AI_NODE */
  TestScope?: string
  /** queued | executing | completed | failed | cancelled */
  TaskStatus?: string
  /** 起始时间（按 CreateTime） */
  StartTime?: string
  /** 结束时间（按 CreateTime） */
  EndTime?: string
}

// ──── 响应契约 ────

/** 列表行 —— 一条执行任务的摘要 */
export interface TaskHistoryItem {
  TaskCode: string
  TaskType: string
  /** FULL | NODE | AI_NODE */
  TestScope: string
  TaskStatus: string
  RuleCode: string
  EnterpriseCode: string
  PhaseCode: string
  DurationMs?: number
  StartedAt?: string
  CompletedAt?: string
  ErrorMessage?: string
  CreateTime?: string
  /** 路径条数（wf_path_execution） */
  PathCount: number
  /** 节点条数（wf_node_execution 去重后） */
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
  /** 本路径复用的节点数（DB 层体现「节点复用」的唯一字段） */
  ReusedCount: number
  NodeIds: string[]
  FailedAtNodeId?: string
  ErrorMessage?: string
  /** 路径最终输出（已解析为对象；超 64KB 时是 { _truncated, _originalBytes, preview } 信封） */
  Output?: any
  DurationMs?: number
  StartedAt?: string
  CompletedAt?: string
}

/** 第四层：节点 */
export interface TaskNodeDetail {
  NodeId: string
  NodeType: string
  NodeTitle: string
  SkillCode: string
  ExecStatus: string
  Output?: any
  ErrorMessage?: string
  StartedAt?: string
  CompletedAt?: string
  ExecutionTimeMs?: number
  /** 0=新执行 1=复用（注意：去重后 DB 里恒为 0，属预期） */
  IsReused: number
}

/** 四层聚合详情 */
export interface TaskExecutionDetail {
  Task: TaskHistoryItem
  Items: TaskDetailItem[]
  Paths: TaskPathDetail[]
  Nodes: TaskNodeDetail[]
}

// ──── API ────

/** 分页查询测试历史 */
export async function getTaskHistory(
  params: TaskHistoryRequest = {},
): Promise<TaskHistoryPage> {
  const res = await yzhApi.post<ApiResponse<TaskHistoryPage>>(
    '/api/Workflow/test/history',
    { Page: 1, PageSize: 20, ...params },
  )
  return res.data
}

/** 查询一次执行的四层聚合详情 */
export async function getTaskDetail(taskCode: string): Promise<TaskExecutionDetail> {
  const res = await yzhApi.get<ApiResponse<TaskExecutionDetail>>(
    `/api/Workflow/test/detail/${encodeURIComponent(taskCode)}`,
  )
  return res.data
}
