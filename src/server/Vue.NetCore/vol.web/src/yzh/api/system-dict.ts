/**
 * 系统字典 API - 体系认证平台自研 API 层
 */
import { yzhApi } from './client'
import type { Page, PageParams } from '../components/table/types'

export interface SysDictionary {
  dic_Id: number
  dicName: string
  dicCode: string
  enable: number
  orderNo?: number
  parentId?: number | null
  creator?: string
  createDate?: string
  modifyDate?: string
  remark?: string
}

export interface SysDictionaryList {
  dicList_Id: number
  dic_Id: number
  dicName: string
  dicValue: string
  orderNo: number
  enable: number
  creator?: string
  createDate?: string
  modifyDate?: string
  remark?: string
}

export interface DictSearchParams extends PageParams {
  dicName?: string
  dicCode?: string
  enable?: number
}

export async function getDictPage(params: DictSearchParams): Promise<Page<SysDictionary>> {
  return yzhApi.post<Page<SysDictionary>>('/api/Sys_Dictionary/getPageData', params)
}

export async function getDict(id: number): Promise<SysDictionary> {
  return yzhApi.get<SysDictionary>('/api/Sys_Dictionary/getDetail', { id })
}

export async function saveDict(dict: Partial<SysDictionary> & { dic_Id?: number }): Promise<void> {
  if (dict.dic_Id) {
    return yzhApi.put<void>('/api/Sys_Dictionary/update', dict)
  }
  return yzhApi.post<void>('/api/Sys_Dictionary/add', dict)
}

export async function deleteDict(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/Sys_Dictionary/delete', { params: { ids: v } })
}

export async function getDictListPage(dicId: number, params: PageParams): Promise<Page<SysDictionaryList>> {
  return yzhApi.post<Page<SysDictionaryList>>('/api/Sys_DictionaryList/getPageData', { ...params, dic_Id: dicId })
}

export async function saveDictListItem(item: Partial<SysDictionaryList> & { dicList_Id?: number }): Promise<void> {
  if (item.dicList_Id) {
    return yzhApi.put<void>('/api/Sys_DictionaryList/update', item)
  }
  return yzhApi.post<void>('/api/Sys_DictionaryList/add', item)
}

export async function deleteDictListItem(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/Sys_DictionaryList/delete', { params: { ids: v } })
}
