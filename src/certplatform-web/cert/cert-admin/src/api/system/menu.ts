import http from '@yzh-core/utils/http'
import type { ApiResponse as HttpApiResponse } from '@yzh-core/utils/http'
import type { PageParams, Page } from '@yzh-core/components/table/types'

// 系统菜单实体
export interface SysMenu {
  id: number
  menuName: string
  menuUrl: string
  menuIcon: string
  sort: number
  enable: number
  parentId?: number
  tableName?: string
}

// 菜单节点（树形结构，供前端组件使用）
export interface MenuNode {
  id: number
  parentId: number
  name: string
  url: string
  icon: string
  children: MenuNode[]
}

// Vol 框架菜单项（扁平结构）
export interface VolMenuItem {
  id: number
  name: string
  url: string
  parentId: number
  icon: string
  enable: number
  tableName?: string
  permission?: string[]
}

// Vol 框架响应 data 结构
interface VolMenuResponse {
  menu: VolMenuItem[]
  asyncApi: string[]
}

/**
 * 获取当前用户的菜单树（基于角色权限）
 * 适配旧 Vol 框架 API: GET /api/Menu/getTreeMenu
 * 将扁平列表转换为树形结构
 */
export function getMenuTree(): Promise<HttpApiResponse<MenuNode[]>> {
  return http.get<VolMenuResponse>('/Menu/getTreeMenu').then((res) => {
    const response = res as unknown as HttpApiResponse<VolMenuResponse>
    const flatMenus = response.data?.menu ?? []
    return {
      code: response.code,
      message: response.message,
      data: buildTree(flatMenus)
    }
  })
}

/**
 * 获取菜单分页列表
 * 后端 API: POST /api/System/MenuManagement/filter
 */
export async function getMenuPage(params: PageParams): Promise<Page<SysMenu>> {
  const res = await http.post<Page<SysMenu>>('/System/MenuManagement/filter', params)
  return res.data
}

/**
 * 将扁平菜单列表转换为树形结构
 */
function buildTree(items: VolMenuItem[]): MenuNode[] {
  const map = new Map<number, MenuNode>()
  const roots: MenuNode[] = []

  // 先创建所有启用的节点
  for (const item of items) {
    if (item.enable !== 1) continue
    map.set(item.id, {
      id: item.id,
      parentId: item.parentId,
      name: item.name,
      url: item.url || '',
      icon: item.icon || '',
      children: []
    })
  }

  // 构建父子关系
  for (const node of map.values()) {
    if (node.parentId === 0 || !map.has(node.parentId)) {
      roots.push(node)
    } else {
      const parent = map.get(node.parentId)
      parent?.children.push(node)
    }
  }

  return roots
}
