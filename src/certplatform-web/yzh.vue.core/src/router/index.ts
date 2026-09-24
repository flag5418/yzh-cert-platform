import type { RouteRecordRaw } from 'vue-router'

/**
 * 原子路由导出 —— 每条 path **单一来源**（契约铁律 #1）
 *
 * 宿主用法（二选一，永不双注册同一 path）：
 *   A. 引用原子路由：...yzhSystemRoutes
 *   B. 手写覆盖：    { path: 'system/user', component: () => import('@/pages/custom-user') }
 *
 * 迁移序：P2 注入 yzhLoginRoute / yzhHomeRoute / createYzhRoutes；
 *        P3~P5 每迁一页向 yzhSystemRoutes 追加一条。
 */

/**
 * 系统管理原子路由（相对 shell 子路由；由 P3~P5 逐页充实）
 * path 恒定 = `Sys_Menu.Url`（对外 URL 永不因下沉而变化）
 */
export const yzhSystemRoutes: RouteRecordRaw[] = []

// ── P2 注入点（应用壳落盘后补全）─────────────────────────────
// export const yzhLoginRoute: RouteRecordRaw = { ... }
// export const yzhHomeRoute: RouteRecordRaw = { ... }
// export function createYzhRoutes(options): RouteRecordRaw[] { ... }
