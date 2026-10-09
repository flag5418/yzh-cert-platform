/**
 * 专家端队列监控 API（对齐后端 ExpertTaskController 的 /queue/* 端点）
 *
 * ★ 工作区隔离：后端 ResolveWorkspace() 自动按当前登录人的工作区过滤，
 *   前端无需传 OrgCode —— 避免跨租户泄露。
 */
import { yzhApi } from '@yzh-core/api/client'
import { expectOk, unwrapOk } from '@yzh-core/utils/apiResponse'
import type { ApiResponse } from '@yzh-core/types'

/** 队列头（对应 cert_expert_task_queue） */
export interface ExpertQueueItem {
  code: string
  queueType: string
  queueName?: string
  scopeKey: string
  taskCode: string
  subTaskCode: string
  standardCode: string
  queueStatus: string
  totalCount: number
  doneCount: number
  failedCount: number
  skippedCount: number
  progress: number
  priority: number
  startTime: string
  finishTime: string
  createTime: string
  createBy: string
  lastError: string
}

/** 队列项（对应 cert_expert_task_queue_item） */
export interface ExpertQueueTaskItem {
  code: string
  taskCode: string
  taskItemCode: string
  itemType: string
  itemCode: string
  seq: number
  payload: string
  itemStatus: string
  errorType: string
  errorMessage: string
  retryCount: number
  startTime: string
  finishTime: string
}

/** 队列监控统计卡 */
export interface ExpertQueueStats {
  running: number
  pending: number
  completed: number
  failed: number
  cancelled: number
  todayTotal: number
  todayCompleted: number
  todayFailed: number
}

/** 队列监控列表响应 */
export interface ExpertQueueListResult {
  total: number
  rows: ExpertQueueItem[]
}

/** 队列列表请求 */
export interface QueueListParams {
  status?: string
  startTime?: string
  endTime?: string
  page: number
  rows: number
}

/** 队列监控列表（工作区隔离由后端保证） */
export async function getExpertQueueList(params: QueueListParams): Promise<ExpertQueueListResult> {
  const res = await yzhApi.post<ApiResponse<ExpertQueueListResult>>('/api/Auditor/ExpertTask/queue/list', {
    Status: params.status || null,
    StartTime: params.startTime || null,
    EndTime: params.endTime || null,
    Page: params.page,
    Rows: params.rows,
  })
  return unwrapOk(res, '获取队列列表失败')
}

/** 队列监控统计卡 */
export async function getExpertQueueStats(): Promise<ExpertQueueStats> {
  const res = await yzhApi.post<ApiResponse<ExpertQueueStats>>('/api/Auditor/ExpertTask/queue/stats')
  return unwrapOk(res, '获取队列统计失败')
}

/** 队列详情 + 子任务列表 */
export async function getExpertQueueDetail(queueCode: string): Promise<{
  queue: ExpertQueueItem
  items: ExpertQueueTaskItem[]
}> {
  const res = await yzhApi.post<ApiResponse<{ queue: ExpertQueueItem; items: ExpertQueueTaskItem[] }>>(
    '/api/Auditor/ExpertTask/queue/detail',
    { QueueCode: queueCode }
  )
  return unwrapOk(res, '获取队列详情失败')
}

/** 取消队列 */
export async function cancelExpertQueue(queueCode: string): Promise<void> {
  expectOk(
    await yzhApi.post<ApiResponse<void>>('/api/Auditor/ExpertTask/queue/cancel', { QueueCode: queueCode }),
    '取消队列失败'
  )
}
