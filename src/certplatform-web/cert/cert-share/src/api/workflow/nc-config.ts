import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/types'

export interface NCRule {
  Id?: number
  Code?: string
  RuleCode: string
  RuleName: string
  RuleNameEn?: string
  ClauseCode: string
  ClauseNumber?: string
  ClauseTitle?: string
  OrgCode?: string
  StandardCode: string
  PhaseCode: string
  IsActive: boolean
  Remark?: string
  CreateTime?: string
}

export interface ISOClause {
  Code: string
  ParentCode?: string
  ClauseNumber: string
  Title: string
  SortOrder?: number
  /** 展示标签（ClauseNumber + Title），组树时注入 */
  Label?: string
  Children?: ISOClause[]
}

// ──── 规则 CRUD ────

/** 分页查询（POST /filter） */
export function getNCRulePage(params: {
  Page: number
  PageSize: number
  SortField?: string
  SortOrder?: string
  Filters: Array<{ Field: string; Value: string; Operator: string }>
}) {
  return yzhApi.post<{ Items: NCRule[]; TotalCount: number }>('/api/ValidationRule/filter', params)
}

/** 新增规则（POST /add） */
export function saveNCRule(data: Partial<NCRule>) {
  return yzhApi.post<NCRule>('/api/ValidationRule/add', data)
}

/** 修改规则（POST /update） */
export function updateNCRule(data: Partial<NCRule>) {
  return yzhApi.post<NCRule>('/api/ValidationRule/update', data)
}

/** 删除规则（POST /delete） */
export function deleteNCRule(codes: string[]) {
  return yzhApi.post('/api/ValidationRule/delete', codes)
}

/** 切换启用状态 */
export function toggleNCRuleActive(code: string) {
  return yzhApi.post(`/api/ValidationRule/toggle-active?code=${code}`)
}

/** 深拷贝规则 */
export function copyNCRule(sourceCode: string) {
  return yzhApi.post(`/api/ValidationRule/copy?sourceCode=${sourceCode}`)
}

// ──── 条款树（由 ISOClauseController 提供） ────

/** 获取条款树（后端返回扁平列表，由前端按 ParentCode 组树） */
export async function getISOClauseTree(standardCode: string): Promise<ISOClause[]> {
  const res = await yzhApi.get<ApiResponse<ISOClause[]>>(
    '/api/Foundation/ISOClause/getTree',
    { standardCode },
  )
  return res?.data ?? []
}
