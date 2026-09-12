import http from '@yzh-core/utils/http'
import type { Result } from '@yzh-core/utils/http'

/**
 * 菜单节点（前端统一 camelCase）
 */
export interface SysMenu {
  id: string
  code: string
  menuName: string
  parentCode: string
  url?: string
  icon?: string
  description?: string
  enable: number
  orderNo: number
  tag?: string
  children?: SysMenu[]
}

/**
 * 后端返回的菜单数据结构（PascalCase）
 */
interface RawSysMenu {
  Id?: string
  Code?: string
  ParentCode?: string
  MenuName?: string
  Url?: string
  Icon?: string
  Description?: string
  Enable?: number
  OrderNo?: number
  Tag?: string
}

/**
 * 将后端 PascalCase 转换为前端 camelCase
 */
function normalizeMenu(raw: RawSysMenu): SysMenu {
  return {
    id: raw.Id ?? '',
    code: raw.Code ?? '',
    menuName: raw.MenuName ?? '',
    parentCode: raw.ParentCode ?? '0',
    url: raw.Url,
    icon: raw.Icon,
    description: raw.Description,
    enable: raw.Enable ?? 1,
    orderNo: raw.OrderNo ?? 0,
    tag: raw.Tag,
  }
}

/**
 * 从扁平列表构建树形结构
 */
function buildTree(rawList: RawSysMenu[]): SysMenu[] {
  const normalized = rawList.map(normalizeMenu)
  const map = new Map<string, SysMenu>()
  const roots: SysMenu[] = []

  // 先初始化每个节点（添加 children 数组）
  for (const item of normalized) {
    map.set(item.code, { ...item, children: [] })
  }

  // 构建父子关系
  for (const item of normalized) {
    const node = map.get(item.code)!
    if (item.parentCode === '0' || !map.has(item.parentCode)) {
      roots.push(node)
    } else {
      const parent = map.get(item.parentCode)
      parent?.children?.push(node)
    }
  }

  // 移除空的 children 属性（前端模板用 v-if 判断）
  const cleanTree = (nodes: SysMenu[]): SysMenu[] =>
    nodes.map(node => {
      const cleaned = { ...node }
      if (cleaned.children?.length === 0) {
        delete cleaned.children
      } else {
        cleaned.children = cleanTree(cleaned.children!)
      }
      return cleaned
    })

  return cleanTree(roots)
}

/**
 * 获取菜单树（自动归一化 + 构建树形结构）
 */
export async function getMenuTree(): Promise<Result<SysMenu[]>> {
  const res = await http.get<Result<RawSysMenu[]>>('/System/MenuManagement/tree')
  if (res.data && Array.isArray(res.data)) {
    res.data = buildTree(res.data)
  }
  return res
}

/**
 * 新增菜单
 */
export function addMenu(data: Partial<SysMenu>): Promise<Result<SysMenu>> {
  return http.post<Result<SysMenu>>('/System/MenuManagement/add', data)
}

/**
 * 修改菜单
 */
export function updateMenu(data: Partial<SysMenu>): Promise<Result<SysMenu>> {
  return http.post<Result<SysMenu>>('/System/MenuManagement/update', data)
}

/**
 * 批量删除菜单
 */
export function deleteMenu(codes: string[]): Promise<Result<number>> {
  return http.post<Result<number>>('/System/MenuManagement/delete', { codes })
}

/**
 * 启用/禁用菜单
 */
export function toggleEnable(code: string, enable: number): Promise<Result<any>> {
  const action = enable === 1 ? 'Enable' : 'Disable'
  return http.post<Result<any>>(`/System/MenuManagement/action/${action}`, { Code: code })
}
