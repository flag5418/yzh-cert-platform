import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface QueueTask {
  id: number
  taskNo: string
  taskType: string
  fileName: string
  fileCode: string
  status: 'pending' | 'processing' | 'success' | 'failed'
  progress: number
  message?: string
  errorCode?: string
  createDate: string
  updateDate?: string
}

export async function getQueueTaskPage(params: PageParams & { taskType?: string; status?: string }): Promise<Page<QueueTask>> {
  return yzhApi.post<Page<QueueTask>>('/api/QueueTask/getPageData', params)
}

export async function cancelQueueTask(id: number): Promise<any> {
  return yzhApi.post(`/api/QueueTask/cancel?id=${id}`)
}

export async function retryQueueTask(id: number): Promise<any> {
  return yzhApi.post(`/api/QueueTask/retry?id=${id}`)
}

export async function getQueueTaskDetail(id: number): Promise<QueueTask> {
  return yzhApi.get<QueueTask>(`/api/QueueTask/getDetail?id=${id}`)
}
