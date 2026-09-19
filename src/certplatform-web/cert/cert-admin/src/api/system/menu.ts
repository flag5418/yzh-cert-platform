import { yzhApi, type ApiResponse } from '@yzh-core/api/client'

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
 * 获取当前用户可见菜单树（侧边栏使用，后端按角色权限过滤）
 */
export async function getMenuTree(): Promise<ApiResponse<SysMenu[]>> {
  const res = await yzhApi.get<ApiResponse<RawSysMenu[]>>('/api/System/MenuManagement/tree')
  return { ...res, data: Array.isArray(res.data) ? buildTree(res.data) : [] }
}

/**
 * 获取全量菜单树（菜单管理页使用，不做角色权限过滤）
 */
export async function getAllMenuTree(): Promise<ApiResponse<SysMenu[]>> {
  const res = await yzhApi.get<ApiResponse<RawSysMenu[]>>('/api/System/MenuManagement/tree/all')
  return { ...res, data: Array.isArray(res.data) ? buildTree(res.data) : [] }
}

/**
 * 新增菜单
 */
export function addMenu(data: Partial<SysMenu>): Promise<ApiResponse<SysMenu>> {
  return yzhApi.post<ApiResponse<SysMenu>>('/api/System/MenuManagement/add', data)
}

/**
 * 修改菜单
 */
export function updateMenu(data: Partial<SysMenu>): Promise<ApiResponse<SysMenu>> {
  return yzhApi.post<ApiResponse<SysMenu>>('/api/System/MenuManagement/update', data)
}

/**
 * 批量删除菜单
 *
 * ⚠️ 后端签名为 Delete([FromBody] string[] codes)，必须直接提交裸数组；
 * 传 { codes: [...] } 无法绑定 → 400「未指定要删除的记录」。
 */
export function deleteMenu(codes: string[]): Promise<ApiResponse<number>> {
  return yzhApi.post<ApiResponse<number>>('/api/System/MenuManagement/delete', codes)
}

/**
 * 启用/禁用菜单
 */
export function toggleEnable(code: string, enable: number): Promise<ApiResponse<any>> {
  const action = enable === 1 ? 'Enable' : 'Disable'
  return yzhApi.post<ApiResponse<any>>(`/api/System/MenuManagement/action/${action}`, { Code: code })
}
