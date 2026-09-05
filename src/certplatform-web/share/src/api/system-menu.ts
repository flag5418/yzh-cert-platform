import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface SysMenu {
  menu_Id: number
  menuName: string
  menuUrl: string
  menuIcon: string
  parentId: number
  sort: number
  enable: number
  createDate: string
}

export async function getMenuPage(params: PageParams): Promise<Page<SysMenu>> {
  return yzhApi.post<Page<SysMenu>>('/api/Sys_Menu/getPageData', params)
}

export async function getMenuTree(): Promise<SysMenu[]> {
  return yzhApi.post<SysMenu[]>('/api/Sys_Menu/getTree')
}
