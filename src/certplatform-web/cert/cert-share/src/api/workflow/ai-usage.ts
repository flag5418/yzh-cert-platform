import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from 'yzh.vue.core/types'

export interface AIUsageSummary {
  TotalCost: number
  MonthCost: number
  WeekCost: number
  TodayCost: number
  TotalCalls: number
  MonthCalls: number
  WeekCalls: number
  TodayCalls: number
}

export interface AIUsageCall {
  Id: number
  Skill: string
  Model: string
  PromptTokens: number
  CompletionTokens: number
  TotalTokens: number
  CostUsd: number
  DurationMs: number
  Success: boolean
  ErrorMessage?: string
  CreateTime: string
}

export interface AIUsageDaily {
  Date: string
  Cost: number
  Calls: number
}

export async function getAIUsageSummary(): Promise<AIUsageSummary> {
  return yzhApi.get<AIUsageSummary>('/api/AIUsage/summary')
}

export async function getAIUsageDaily(startDate: string, endDate: string): Promise<AIUsageDaily[]> {
  // get 的第二个参数就是查询对象（不要再包一层 params）
  return yzhApi.get<AIUsageDaily[]>('/api/AIUsage/daily-costs', { startDate, endDate })
}

/**
 * 获取 AI 用量分页数据
 * @deprecated 后端 AIUsageController 尚未提供 /filter 端点，当前路径为临时占位
 * TODO: 后端提供 /filter 端点后，改为 /api/AIUsage/filter
 */
export async function getAIUsageCalls(params: PageParams & { startDate?: string; endDate?: string }): Promise<Page<AIUsageCall>> {
  return yzhApi.post<Page<AIUsageCall>>('/api/AIUsage/getPageData', params)
}

export async function getAliyunStatus(): Promise<{ configured: boolean; dashboardUrl?: string }> {
  return yzhApi.get('/api/AIUsage/aliyun-status')
}
