import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface SysDept {
  dept_Id: string
  deptName: string
  parent_Id?: string
  sort: number
  enable: number
  createDate: string
}

export async function getDeptPage(params: PageParams): Promise<Page<SysDept>> {
  return yzhApi.post<Page<SysDept>>('/api/Sys_Department/getPageData', params)
}
