import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/types'
import type { ISOClause } from '../../types/cert'

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

// ISOClause 复用 @share/types/cert 的定义（含 ParentCode/SortOrder 等全量字段），
// 此处补充树形展示专用字段，避免与 api/cert/iso-clause 导出同名冲突（TS2308）
export interface ISOClauseTreeNode {
  Code: string
  ParentCode?: string
  ClauseNumber: string
  Title: string
  SortOrder?: number
  /** 展示标签（ClauseNumber + Title），组树时注入 */
  Label?: string
  Children?: ISOClauseTreeNode[]
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
  return yzhApi.post<{ data: { Items: NCRule[]; TotalCount: number } }>('/api/ValidationRule/filter', params)
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

// ⚠️ 切换启用/复制规则已迁至标准行自定义操作约定：
//    POST /api/ValidationRule/action/{ToggleActive|Copy}
//    按钮由 Cert/ValidationRule.json 的 RowButtons.CustomButtons 配置驱动，
//    前端经 SingleTableCore.dispatch('custom:{method}') 直达，无需手写 api 函数。

// ──── 条款树（由 ISOClauseController 提供） ────

/** 获取条款树（后端返回扁平列表，由前端按 ParentCode 组树） */
export async function getISOClauseTree(standardCode: string): Promise<ISOClauseTreeNode[]> {
  const res = await yzhApi.get<ApiResponse<ISOClause[]>>(
    '/api/Foundation/ISOClause/getTree',
    { standardCode },
  )
  return (res?.data ?? []) as ISOClauseTreeNode[]
}
