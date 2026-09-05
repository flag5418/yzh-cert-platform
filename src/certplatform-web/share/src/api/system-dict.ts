import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface SysDict {
  dict_Id: number
  dictName: string
  dictCode: string
  enable: number
  createDate: string
}

export interface SysDictList {
  dictList_Id: number
  dict_Id: number
  listText: string
  listValue: string
  listSort: number
  enable: number
}

export async function getDictPage(params: PageParams): Promise<Page<SysDict>> {
  return yzhApi.post<Page<SysDict>>('/api/Sys_Dictionary/getPageData', params)
}

export async function getDictList(dictId: number): Promise<SysDictList[]> {
  return yzhApi.post<SysDictList[]>('/api/Sys_DictionaryList/getList', { dictId })
}
