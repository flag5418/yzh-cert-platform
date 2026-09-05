import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface NCRule {
  id: number
  ruleCode: string
  ruleName: string
  ruleNameEn?: string
  clauseCode: string
  clauseNumber?: string
  orgCode: string
  standardCode: string
  phaseCode: string
  isActive: boolean
  remark?: string
  createDate?: string
}

export interface ISOClause {
  code: string
  clauseNumber: string
  title: string
  children?: ISOClause[]
}

export interface ISOClauseTree {
  label: string
  value: string
  children?: ISOClauseTree[]
}

export async function getNCRulePage(params: PageParams, filters?: any): Promise<Page<NCRule>> {
  return yzhApi.post<Page<NCRule>>('/api/ValidationRule/getPageData', params, { params: filters })
}

export async function getNCRule(ruleCode: string): Promise<NCRule> {
  return yzhApi.get<NCRule>(`/api/ValidationRule/getDetail?ruleCode=${ruleCode}`)
}

export async function saveNCRule(data: Partial<NCRule>): Promise<any> {
  return yzhApi.post('/api/ValidationRule/save', data)
}

export async function deleteNCRule(id: number): Promise<any> {
  return yzhApi.post(`/api/ValidationRule/delete?id=${id}`)
}

export async function toggleNCRuleActive(id: number): Promise<any> {
  return yzhApi.post(`/api/ValidationRule/toggleActive?id=${id}`)
}

export async function getISOClauseTree(standardCode: string): Promise<ISOClause[]> {
  return yzhApi.get<ISOClause[]>(`/api/ISOClause/getTree?standardCode=${standardCode}`)
}
