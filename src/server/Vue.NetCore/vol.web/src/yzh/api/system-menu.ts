/**
 * 系统菜单 API - 体系认证平台自研 API 层
 */
import { yzhApi } from './client'

export interface SysMenu {
  /** 菜单 ID */
  id: number
  /** 菜单名称 */
  name: string
  /** 父级 ID */
  parentId?: number | null
  /** 路由 URL */
  url?: string | null
  /** 图标 */
  icon?: string | null
  /** 是否启用 */
  enable: number
  /** 表名（权限关联） */
  tableName?: string | null
  /** 权限列表 */
  permission?: string[]
  /** 子菜单 */
  children?: SysMenu[]
}

export async function getMenuTree(): Promise<SysMenu[]> {
  // Vol 的 getTreeMenu 返回 { menu: [...], asyncApi: [...] }
  const result = await yzhApi.get<{ menu: SysMenu[] }>('/api/menu/getTreeMenu')
  return result.menu
}

export async function getMenu(id: number): Promise<SysMenu> {
  return yzhApi.get<SysMenu>('/api/menu/getDetail', { id })
}

export async function saveMenu(menu: Partial<SysMenu> & { id?: number }): Promise<void> {
  if (menu.id) {
    return yzhApi.put<void>('/api/menu/update', menu)
  }
  return yzhApi.post<void>('/api/menu/add', menu)
}

export async function deleteMenu(ids: number | number[]): Promise<void> {
  const v = Array.isArray(ids) ? ids.join(',') : ids
  return yzhApi.delete<void>('/api/menu/delete', { params: { ids: v } })
}
