import http from '@yzh-core/utils/http'
import type { ApiResponse as HttpApiResponse } from '@yzh-core/utils/http'

// 菜单节点（双键设计：Id + Code）
export interface MenuNode {
  id: number
  parentId: number
  code: string
  name: string
  url: string
  icon: string
  tag: string
  orderNo: number
  children: MenuNode[]
}

// 菜单树响应
export interface MenuTreeResponse {
  menu: MenuNode[]
}

/**
 * 获取当前用户的菜单树（基于角色权限）
 * 后端 API: GET /api/Menu/tree
 */
export async function getMenuTree(): Promise<HttpApiResponse<MenuTreeResponse>> {
  return http.get('/Menu/tree')
}
