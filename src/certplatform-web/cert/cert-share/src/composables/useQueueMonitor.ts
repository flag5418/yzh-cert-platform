/**
 * useQueueMonitor — 队列中心 API 封装
 * 对齐后端 QueueMonitorController (/api/System/QueueMonitor/*)
 */
import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/types'
import type { QueueStatus } from '@share/types'

export interface QueueListParams {
  type?: string
  status?: string
  startTime?: string
  endTime?: string
  page?: number
  rows?: number
}

export interface QueueListItem {
  queueCode: string
  queueType: string
  queueName: string
  scopeKey: string
  status: string
  totalCount: number
  completedCount: number
  failedCount: number
  processingCount: number
  pendingCount: number
  cancelledCount: number
  progress: number
  createBy: string
  sourceType: string
  sourceId: string
  startTime: string
  endTime: string
  createTime: string
}

export interface QueueListResult {
  total: number
  rows: QueueListItem[]
}

export interface QueueDetailTask {
  id: number
  taskNo: number
  taskType: string
  fileCode: string
  fileName: string
  convertType: string
  status: string
  retryCount: number
  errorType: string
  errorMessage: string
  processTime: string
  completeTime: string
}

export interface QueueDetailLock {
  code: string
  resourceTable: string
  resourceCode: string
  resourceName: string
  taskNo: number
  status: string
  createTime: string
  releaseTime: string
}

export interface QueueDetailResult {
  queue: QueueListItem
  tasks: QueueDetailTask[]
  locks: QueueDetailLock[]
}

/** 队列主表分页（Tabs + 时间过滤） */
export async function getQueueList(params: QueueListParams = {}): Promise<QueueListResult> {
  const res = await yzhApi.post<ApiResponse<QueueListResult>>('/api/System/QueueMonitor/list', {
    Type: params.type || '',
    Status: params.status || '',
    StartTime: params.startTime || null,
    EndTime: params.endTime || null,
    Page: params.page || 1,
    Rows: params.rows || 20,
  })
  return res.data!
}

/** 队列统计卡 */
export async function getQueueStatus(): Promise<QueueStatus> {
  const res = await yzhApi.post<ApiResponse<QueueStatus>>('/api/System/QueueMonitor/status')
  return res.data!
}

/** 队列详情（主表 + 子任务 + 资源锁） */
export async function getQueueDetail(queueCode: string): Promise<QueueDetailResult> {
  const res = await yzhApi.post<ApiResponse<QueueDetailResult>>('/api/System/QueueMonitor/detail', { QueueCode: queueCode })
  return res.data!
}

/** 取消队列 */
export async function cancelQueue(queueCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/System/QueueMonitor/cancel', { QueueCode: queueCode })
}

/** 整队重跑 */
export async function retryQueue(queueCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/System/QueueMonitor/retry', { QueueCode: queueCode })
}

/** 单个子任务重试（准则 A：业务键 TaskCode） */
export async function retryTask(taskCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/System/QueueMonitor/task/retry', { TaskCode: taskCode })
}

/** 查找资源锁 */
export async function findResourceLock(resourceTable: string, resourceCodes: string[]): Promise<any[]> {
  const res = await yzhApi.post<ApiResponse<any[]>>('/api/System/QueueMonitor/resource/locked', {
    ResourceTable: resourceTable,
    ResourceCodes: resourceCodes,
  })
  return res.data!
}
