import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'
import type { Enterprise } from '../../types/cert'

/**
 * @file 企业 API
 * @status 占位 - 后端 EnterpriseController 尚未提供 /filter 端点
 * TODO: 后端提供 /filter 端点后，路径改为 /api/Enterprise/filter
 */

/**
 * 获取企业分页数据
 * @deprecated 后端 EnterpriseController 尚未提供 /filter 端点，当前路径为临时占位
 */
export async function getEnterprisePage(params: PageParams): Promise<Page<Enterprise>> {
  return yzhApi.post<Page<Enterprise>>('/api/Enterprise/getPageData', params)
}

export async function addEnterprise(data: Partial<Enterprise>): Promise<any> {
  return yzhApi.post('/api/Enterprise/add', data)
}

export async function updateEnterprise(data: Partial<Enterprise>): Promise<any> {
  return yzhApi.post('/api/Enterprise/update', data)
}

/**
 * @deprecated 占位 — 后端无 Enterprise 控制器。删除传业务键 Code（准则 A）
 */
export async function deleteEnterprise(code: string): Promise<any> {
  return yzhApi.post(`/api/Enterprise/delete`, null, { params: { code } })
}
