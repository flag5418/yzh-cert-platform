import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from 'yzh.vue.core/types'

export interface AIUsageSummary {
  totalCost: number
  monthCost: number
  weekCost: number
  todayCost: number
  totalCalls: number
  monthCalls: number
  weekCalls: number
  todayCalls: number
}

export interface AIUsageCall {
  id: number
  skill: string
  model: string
  promptTokens: number
  completionTokens: number
  totalTokens: number
  costUsd: number
  durationMs: number
  success: boolean
  errorMessage?: string
  createDate: string
}

export interface AIUsageDaily {
  date: string
  cost: number
  calls: number
}

export async function getAIUsageSummary(): Promise<AIUsageSummary> {
  return yzhApi.get<AIUsageSummary>('/api/AIUsage/summary')
}

export async function getAIUsageDaily(startDate: string, endDate: string): Promise<AIUsageDaily[]> {
  return yzhApi.get<AIUsageDaily[]>('/api/AIUsage/daily-costs', { params: { startDate, endDate } })
}

export async function getAIUsageCalls(params: PageParams & { startDate?: string; endDate?: string }): Promise<Page<AIUsageCall>> {
  return yzhApi.post<Page<AIUsageCall>>('/api/AIUsage/getPageData', params)
}

export async function getAliyunStatus(): Promise<{ configured: boolean; dashboardUrl?: string }> {
  return yzhApi.get('/api/AIUsage/aliyun-status')
}
