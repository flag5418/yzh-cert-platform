import type { RouteRecordRaw, RouteRecordSingleView } from 'vue-router'

/**
 * 原子路由导出 —— 每条 path **单一来源**（契约铁律 #1）
 *
 * 宿主用法（二选一，永不双注册同一 path）：
 *   A. 引用原子路由：createYzhRoutes({ ... }) 或 ...yzhSystemRoutes
 *   B. 手写覆盖：    { path: '/login', component: () => import('@/layouts/MyLogin.vue') }
 *
 * 迁移序：P3~P5 每迁一页向 yzhSystemRoutes 追加一条。
 *
 * 类型注：登录/首页用 `RouteRecordSingleView`（而非 `RouteRecordRaw` 联合）——
 * 联合类型对象展开会把 props 误判进 MultipleViews 的具名视图分支导致 TS2345。
 */

/** 登录页原子路由（path 恒定 '/login'） */
export const yzhLoginRoute: RouteRecordSingleView = {
  path: '/login',
  name: 'YzhLogin',
  component: () => import('../pages/auth/Login.vue')
}

/** 默认首页子路由（挂在 shell 下，匹配 '/'；须配合 YzhAppLayout 父路由） */
export const yzhHomeRoute: RouteRecordSingleView = {
  path: '',
  name: 'YzhHome',
  component: () => import('../pages/home/Home.vue')
}

/**
 * 系统管理原子路由（相对 shell 子路由；P3~P5 逐页充实）
 * path 恒定 = `Sys_Menu.Url`（对外 URL 永不因下沉而变化）
 *
 * P3 Wave A 已迁：log / config / role / user
 * P4 Wave B：role-user / role-menu / role-api / menu / api
 */
export const yzhSystemRoutes: RouteRecordRaw[] = [
  { path: 'system/log', name: 'SystemLog', component: () => import('../pages/system/log/index.vue') },
  { path: 'system/config', name: 'SystemConfig', component: () => import('../pages/system/config/index.vue') },
  { path: 'system/role', name: 'SystemRole', component: () => import('../pages/system/role/index.vue') },
  { path: 'system/user', name: 'SystemUser', component: () => import('../pages/system/user/index.vue') },
  { path: 'system/role-user', name: 'SystemRoleUser', component: () => import('../pages/system/role-user/index.vue') },
  { path: 'system/role-menu', name: 'SystemRoleMenu', component: () => import('../pages/system/role-menu/index.vue') },
  { path: 'system/role-api', name: 'SystemRoleApi', component: () => import('../pages/system/role-api/index.vue') },
  { path: 'system/menu', name: 'SystemMenu', component: () => import('../pages/system/menu/index.vue') },
]

export interface YzhRoutesOptions {
  /** 侧栏菜单 Tag 分流（缺省 'admin'） */
  menuTag?: string
  /** 品牌注入（YzhAppLayout props + 登录页 props） */
  branding?: {
    logoText?: string
    appTitle?: string
    login?: {
      appLogo?: string
      appTitle?: string
      appSubtitle?: string
      footerText?: string
    }
  }
  /** 宿主业务路由（foundation/workflow 等手写页面，shell 子路由） */
  businessRoutes?: RouteRecordRaw[]
  /**
   * 默认首页：
   * - true（缺省）→ core 占位首页（欢迎 + 快捷入口）
   * - false        → 不注册 '' 子路由（宿主自供，须保证有匹配 '/' 的子路由或 redirect）
   * - RouteRecordRaw → 自定义 '' 子路由（业务仪表盘）
   */
  home?: boolean | RouteRecordRaw
  /** 是否包含登录页原子路由（宿主整页手写 /login 时传 false） */
  login?: boolean
}

/**
 * 组装宿主应用路由表（登录 + shell + 首页 + 系统页 + 业务页）
 *
 * @example
 * const routes = createYzhRoutes({
 *   menuTag: 'admin',
 *   branding: { appTitle: '映智汇认证管理平台' },
 *   businessRoutes: [{ path: 'cert/iso-standard', component: () => import('@/pages/foundation/iso-standard') }],
 * })
 */
export function createYzhRoutes(options: YzhRoutesOptions = {}): RouteRecordRaw[] {
  const {
    menuTag = 'admin',
    branding = {},
    businessRoutes = [],
    home = true,
    login = true,
  } = options

  const children: RouteRecordRaw[] = []
  if (home === true) {
    children.push({ ...yzhHomeRoute })
  } else if (home !== false) {
    children.push(home)
  }
  children.push(...yzhSystemRoutes, ...businessRoutes)

  const routes: RouteRecordRaw[] = []

  if (login) {
    routes.push({
      ...yzhLoginRoute,
      ...(branding.login ? { props: branding.login } : {}),
    })
  }

  routes.push({
    path: '/',
    name: 'YzhShell',
    component: () => import('../layouts/YzhAppLayout.vue'),
    props: {
      menuTag,
      ...(branding.logoText !== undefined ? { logoText: branding.logoText } : {}),
      ...(branding.appTitle !== undefined ? { appTitle: branding.appTitle } : {}),
    },
    children,
  })

  return routes
}
