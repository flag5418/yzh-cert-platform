import type { SysMenu } from '../api/system/menu'

/**
 * 菜单工具 —— **admin + auditor 的唯一实现**（勿在任一 App 内重复定义）
 *
 * 为什么放在共享层：`AdminLayout.vue` 与 `AuditorLayout.vue` 的侧边栏渲染逻辑、图标映射、
 * Tag 分流规则完全同构。历史上 `ICON_NAME_MAP` + `formatIcon` 只存在于 cert-admin，
 * 专家端若各写一份，日后改图标规则就要改两处（本项目已有「工作流设计器复制两份」的教训）。
 */

/**
 * 历史图标名（Element UI / iView 时代）→ Element Plus 组件名
 *
 * 背景：历史 Vol 项目菜单表存的是 `s-home` / `office-building` 这类旧图标名；
 * 新菜单表已直接存 Element Plus 的 PascalCase 名（如 `Odometer`）。
 * 两条路径都要兼容，故保留映射表 + PascalCase 直通。
 */
export const MENU_ICON_MAP: Record<string, string> = {
  // 系统管理
  'setting': 'Setting',
  's-home': 'HomeFilled',
  'user-solid': 'UserFilled',
  'menu': 'Menu',
  'connection': 'Connection',
  'folder': 'Folder',
  'link': 'Link',
  'receiving': 'Collection',
  'document': 'Document',
  's-tools': 'Tools',
  // 业务管理
  'document-checked': 'DocumentChecked',
  'office-building': 'OfficeBuilding',
  'date': 'Date',
  'document-copy': 'DocumentCopy',
  'operation': 'Operation',
  'files': 'Files',
  'tickets': 'Tickets',
  'collection': 'Collection',
  'chat-line-round': 'ChatLineRound',
  'cpu': 'Cpu',
  'edit': 'Edit',
  'warning': 'Warning',
  'set-up': 'SetUp',
  'money': 'Money',
  's-data': 'DataAnalysis',
}

/** 兜底图标（映射不到时使用） */
export const DEFAULT_MENU_ICON = 'Menu'

/**
 * 图标名归一化 → Element Plus 组件名
 *
 * ⚠️ 返回值是**组件名字符串**，模板里必须配合 `<component :is="...">`，
 *    且宿主 App 必须在 `main.ts` 全局注册 Element Plus 图标：
 *      for (const [key, component] of Object.entries(ElementPlusIconsVue)) app.component(key, component)
 *    否则 Vue 会刷 `Failed to resolve component: Xxx` 警告 —— 不报错，但会污染控制台，
 *    导致调试时把真实问题淹没在噪音里。
 */
export function formatMenuIcon(iconName?: string | null): string {
  if (!iconName) return DEFAULT_MENU_ICON

  // 移除常见前缀
  const name = iconName
    .replace(/^el-icon-/, '')
    .replace(/^ivu-icon ivu-icon-/, '')

  // 优先使用映射表
  if (MENU_ICON_MAP[name]) {
    return MENU_ICON_MAP[name]
  }

  // 尝试直接转换 PascalCase（可能已是 Element Plus 名称）
  const pascalName = name
    .split('-')
    .map((s) => s.charAt(0).toUpperCase() + s.slice(1))
    .join('')

  if (pascalName.length > 0 && /^[A-Z]/.test(pascalName)) {
    return pascalName
  }

  return DEFAULT_MENU_ICON
}

/**
 * 按 `Tag` 分流菜单树（只保留 `tag` 匹配或未打标的节点）
 *
 * ★ 为什么前端必须做这层分流：后端权限过滤**只认 `Sys_RoleMenu`**，
 *   `Sys_Menu.Tag` 完全不参与过滤。而**超管会绕过一切授权**拿到全量菜单 ——
 *   不过滤的话，管理端侧边栏会出现专家端（`Tag='auditor'`）的菜单项，
 *   点击后管理端路由不存在 → 白屏。
 *
 * ⚠️ 未打标（`tag` 为空）的节点**保留** —— 历史菜单可能没有 Tag，
 *    宁可在前端多显示，也不要让存量菜单凭空消失。
 *
 * ⚠️ 菜单树**只渲染 2 层**（`el-sub-menu` + `el-menu-item`），更深层级无效。
 */
export function filterMenuTreeByTag(menus: SysMenu[], tag: string): SysMenu[] {
  const keep = (m: SysMenu) => !m.tag || m.tag === tag
  return menus.filter(keep).map((m) => {
    if (!m.children?.length) return m
    const children = filterMenuTreeByTag(m.children, tag)
    return children.length ? { ...m, children } : m
  })
}
