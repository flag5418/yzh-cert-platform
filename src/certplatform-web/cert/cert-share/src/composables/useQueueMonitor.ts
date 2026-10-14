/**
 * useQueueMonitor — 队列中心 API 封装
 * 对齐后端 QueueMonitorController (/api/System/QueueMonitor/*)
 */
import { yzhApi } from '@yzh-core/api/client'
import { expectOk, unwrapOk } from '@yzh-core/utils/apiResponse'
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
  /** ★ 后端已解析的范围人话（企业名 / 标准名 / 阶段名）；为空则前端回落显示 scopeKey */
  scopeDisplay: string
  status: string
  totalCount: number
  completedCount: number
  failedCount: number
  processingCount: number
  pendingCount: number
  cancelledCount: number
  progress: number
  /**
   * 创建人用户 Code（= `yzh_queue.CreateBy`，契约：存 Code）
   * ⚠️ ⛔ 不要直接当人名渲染（会显示成 `df6e03e9…`）—— 用 `createByName`
   */
  createBy: string
  /** ★ 后端已解析的创建人姓名（查不到 / 无创建人 = 空串 ⇒ 回落显示 createBy 或「—」） */
  createByName: string
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
  /** 业务键 `YzhQueueTask.Code`（单任务重试 `task/retry` 用；⛔ 不用数字 id） */
  code: string
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
  const res = await yzhApi.post<ApiResponse<QueueListResult>>('/api/Admin/System/QueueMonitor/list', {
    Type: params.type || '',
    Status: params.status || '',
    StartTime: params.startTime || null,
    EndTime: params.endTime || null,
    Page: params.page || 1,
    Rows: params.rows || 20,
  })
  return unwrapOk(res, '获取队列列表失败')
}

/** 队列统计卡 */
export async function getQueueStatus(): Promise<QueueStatus> {
  const res = await yzhApi.post<ApiResponse<QueueStatus>>('/api/Admin/System/QueueMonitor/status')
  return unwrapOk(res, '获取队列统计失败')
}

/** 队列详情（主表 + 子任务 + 资源锁） */
export async function getQueueDetail(queueCode: string): Promise<QueueDetailResult> {
  const res = await yzhApi.post<ApiResponse<QueueDetailResult>>('/api/Admin/System/QueueMonitor/detail', { QueueCode: queueCode })
  return unwrapOk(res, '获取队列详情失败')
}

/** 取消队列 */
export async function cancelQueue(queueCode: string): Promise<void> {
  expectOk(
    await yzhApi.post<ApiResponse<void>>('/api/Admin/System/QueueMonitor/cancel', { QueueCode: queueCode }),
    '取消队列失败',
  )
}

/** 整队重跑 */
export async function retryQueue(queueCode: string): Promise<void> {
  expectOk(
    await yzhApi.post<ApiResponse<void>>('/api/Admin/System/QueueMonitor/retry', { QueueCode: queueCode }),
    '重试队列失败',
  )
}

/** 单个子任务重试（准则 A：业务键 TaskCode） */
export async function retryTask(taskCode: string): Promise<void> {
  expectOk(
    await yzhApi.post<ApiResponse<void>>('/api/Admin/System/QueueMonitor/task/retry', { TaskCode: taskCode }),
    '重试子任务失败',
  )
}

/** 查找资源锁 */
export async function findResourceLock(resourceTable: string, resourceCodes: string[]): Promise<any[]> {
  const res = await yzhApi.post<ApiResponse<any[]>>('/api/Admin/System/QueueMonitor/resource/locked', {
    ResourceTable: resourceTable,
    ResourceCodes: resourceCodes,
  })
  return unwrapOk(res, '查询资源锁失败')
}
