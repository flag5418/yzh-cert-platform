import type { SysMenu } from '../api/system/menu'

/**
 * 菜单工具 —— **admin + auditor 唯一实现**（勿在 App 内重复定义）
 *
 * ⚠️ 为什么放在共享层：管理端（AdminLayout）与专家端（AuditorLayout）需要**完全相同**的
 *    图标名归一化与分类标签过滤逻辑。项目此前在工作流设计器上已吃过"两个包装层各写一遍"
 *    的亏 —— 任何新增能力都必须建在共享层，让两端自动同时获得。
 *
 * ⚠️ 图标使用前提：`formatMenuIcon` 返回的是**组件名字符串**，模板必须配合
 *    `<component :is="formatMenuIcon(icon)" />`，且宿主 App 必须在 `main.ts` 全局注册
 *    Element Plus 图标：
 *      for (const [key, c] of Object.entries(ElementPlusIconsVue)) app.component(key, c)
 *    否则 Vue 会刷 "Failed to resolve component: Xxx" 警告 —— 不报错，但污染控制台。
 */

/**
 * 历史图标名 → Element Plus 组件名
 *
 * 背景：历史 Vol 项目的 `Sys_Menu.Icon` 存的是 Element UI / iView 时代命名
 * （`s-home` / `office-building` / `chat-line-round` …）。迁移后需做名称映射；
 * 新建菜单则直接存 Element Plus 的 PascalCase 名（走 `formatMenuIcon` 的直通分支）。
 */
export const MENU_ICON_MAP: Record<string, string> = {
  // 系统管理
  setting: 'Setting',
  's-home': 'HomeFilled',
  'user-solid': 'UserFilled',
  menu: 'Menu',
  connection: 'Connection',
  folder: 'Folder',
  link: 'Link',
  receiving: 'Collection',
  document: 'Document',
  's-tools': 'Tools',
  // 业务管理
  'document-checked': 'DocumentChecked',
  'office-building': 'OfficeBuilding',
  date: 'Date',
  'document-copy': 'DocumentCopy',
  operation: 'Operation',
  files: 'Files',
  tickets: 'Tickets',
  collection: 'Collection',
  'chat-line-round': 'ChatLineRound',
  cpu: 'Cpu',
  edit: 'Edit',
  warning: 'Warning',
  'set-up': 'SetUp',
  money: 'Money',
  's-data': 'DataAnalysis',
}

/** 兜底图标（映射不到时使用） */
export const DEFAULT_MENU_ICON = 'Menu'

/**
 * 图标名归一化：`el-icon-xxx` / `s-home` / `Odometer` → Element Plus 组件名
 */
export function formatMenuIcon(iconName?: string | null): string {
  if (!iconName) return DEFAULT_MENU_ICON

  // 移除常见前缀
  const name = iconName.replace(/^el-icon-/, '').replace(/^ivu-icon ivu-icon-/, '')

  // 优先使用映射表
  if (MENU_ICON_MAP[name]) return MENU_ICON_MAP[name]

  // 直通：可能已是 Element Plus 的 PascalCase 名
  const pascalName = name
    .split('-')
    .map((s) => s.charAt(0).toUpperCase() + s.slice(1))
    .join('')

  if (pascalName.length > 0 && /^[A-Z]/.test(pascalName)) return pascalName

  return DEFAULT_MENU_ICON
}

/**
 * 按分类标签过滤菜单树（**递归**，保留无标签节点）
 *
 * 用途：`Sys_Menu.Tag` 是纯分类标签，**不参与后端权限过滤**（过滤只认 `Sys_RoleMenu`）。
 *      但超管会绕过授权拿到**全量**菜单 —— 若不做前端分流，超管在管理端侧边栏会看到
 *      「专家系统」分组，点进去全部 404。故：
 *        · cert-admin   → `filterMenuTreeByTag(menus, 'admin')`
 *        · cert-auditor → `filterMenuTreeByTag(menus, 'auditor')`
 *
 * 保留 `tag` 为空的节点：历史菜单可能未打标，宁可多显示也不静默隐藏。
 *
 * @param menus 菜单树（`buildTree()` 的产物）
 * @param tag   目标分类标签
 */
export function filterMenuTreeByTag(menus: SysMenu[], tag: string): SysMenu[] {
  const keep = (m: SysMenu) => !m.tag || m.tag === tag

  return menus.filter(keep).map((m) => {
    if (!m.children?.length) return { ...m }
    return { ...m, children: filterMenuTreeByTag(m.children, tag) }
  })
}
