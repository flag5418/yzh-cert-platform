import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/store/auth'

/**
 * 路由表
 *
 * ★ 路由是**静态**的，菜单是**动态**的（来自 `/api/System/MenuManagement/tree`）。
 *   因此每个菜单 `Url` 必须在下面有对应的子路由，否则点击菜单会白屏。
 *   菜单与路由的对应关系见 `scripts/db/20260923_auditor_menu_seed_V1.sql`。
 *
 * ⚠️ 侧边栏只渲染 2 层菜单，更深层级由页面内 Tab / 子路由承载
 *    （如 `/settings` 内部的 个人信息 / 账户与用量 / 消息设置 / 操作日志）。
 */
const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/layouts/AuditorLogin.vue'),
    meta: { anonymous: true }
  },
  {
    path: '/register',
    name: 'Register',
    component: () => import('@/pages/register/index.vue'),
    meta: { anonymous: true }
  },
  {
    path: '/',
    name: 'Home',
    component: () => import('@/layouts/AuditorLayout.vue'),
    redirect: '/overview',
    children: [
      {
        path: 'overview',
        name: 'Overview',
        component: () => import('@/pages/overview/index.vue')
      },
      {
        path: 'enterprises',
        name: 'Enterprises',
        component: () => import('@/pages/enterprises/index.vue')
      },
      {
        path: 'enterprise-stages',
        name: 'EnterpriseStages',
        component: () => import('@/pages/enterprise-stages/index.vue')
      },
      {
        path: 'tasks',
        name: 'Tasks',
        component: () => import('@/pages/tasks/index.vue')
      },
      // ★ 任务建立向导（`/tasks/create` 必须排在 `/tasks/:code` 之前）
      {
        path: 'tasks/create',
        name: 'TaskCreate',
        component: () => import('@/pages/tasks/create.vue')
      },
      // ★ 任务详情（4 Tab：标准子任务 / 执行队列 / 运行日志 / 数据缺口）
      {
        path: 'tasks/:code',
        name: 'TaskDetail',
        component: () => import('@/pages/tasks/detail.vue')
      },
      // ★ 结果两个菜单（左树右表 + 审批 + 导出）
      {
        path: 'nc-results',
        name: 'NcResults',
        component: () => import('@/pages/nc-results/index.vue')
      },
      {
        path: 'report-results',
        name: 'ReportResults',
        component: () => import('@/pages/report-results/index.vue')
      },
      // ★ 企业全局参数定义（MENU_AUD_10）—— 企业档案 × 后台参数定义（标准核心字段）的关联补值清单
      //   ⛔ 无「填充预览」（2026-10-06 裁决）：预览 UI 归企业资料规范化册承载
      {
        path: 'enterprise-fill-params',
        name: 'EnterpriseFillParams',
        component: () => import('@/pages/enterprise-fill-params/index.vue')
      },
      {
        path: 'resources',
        name: 'Resources',
        component: () => import('@/pages/resources/index.vue')
      },
      // ★ 企业原始资料管理（MENU_AUD_11，36 号）—— 企业散乱原始资料 → 双产物 → 语义分析 → L3 画像
      //   ⛔ 必须与菜单 MENU_AUD_11 成对落地：本端路由是静态的，菜单 Url 无对应子路由 ⇒ 点击白屏
      //     且守卫 R12 报「菜单 Url 无对应路由」。当前为 P0 占位页，P3 替换页面内容。
      {
        path: 'enterprise-original',
        name: 'EnterpriseOriginal',
        component: () => import('@/pages/enterprise-original/index.vue')
      },
      // ★ 企业资料规范化（MENU_AUD_12，54 §6.1）—— 企业原始资料 → 规范化标准文档
      //   ⛔ 必须与菜单 MENU_AUD_12 成对落地（菜单 Url 无子路由 ⇒ 点击白屏 + 守卫 R12 报错）。
      //   ★★ 2026-10-11 用户裁决：本页**收窄为「任务列表」**（开任务 / 看进度 / 取消 / 重新执行）；
      //      范围勾选与干跑预览搬到向导 `/enterprise-normalize/create`，
      //      结果审核搬到 `/enterprise-review`（MENU_AUD_14）。
      {
        path: 'enterprise-normalize',
        name: 'EnterpriseNormalize',
        component: () => import('@/pages/enterprise-normalize/index.vue')
      },
      // ★ 规范化任务建立向导（`/enterprise-normalize/create` 必须排在 `/enterprise-normalize` 之前）
      //   ⛔ 不进侧边栏：它是任务列表页的子页面，入口在列表工具栏「创建规范化任务」
      //      与行按钮「重新执行」（带 `?from={queueCode}`）。
      //      ⇒ 已在 `scripts/guards.mjs` 的 ORPHAN_ALLOW 登记（R12 要求显式登记 + 理由）。
      {
        path: 'enterprise-normalize/create',
        name: 'EnterpriseNormalizeCreate',
        component: () => import('@/pages/enterprise-normalize/create.vue')
      },
      // ★ 企业资料审核（MENU_AUD_14）—— 左树（企业 › 阶段 › 任务）+ 右表（已规范化文档）
      //   + 按阶段统一导出 + 逐锚点改值 / 钉住（审核抽屉）。
      //   ⛔ 必须与菜单 MENU_AUD_14 成对落地：本端路由是静态的，菜单 Url 无子路由 ⇒ 点击白屏。
      {
        path: 'enterprise-review',
        name: 'EnterpriseReview',
        component: () => import('@/pages/enterprise-review/index.vue')
      },
      {
        path: 'organization',
        name: 'Organization',
        component: () => import('@/pages/organization/index.vue')
      },
      {
        path: 'queue',
        name: 'QueueMonitor',
        component: () => import('@/pages/queue/index.vue')
      },
      {
        path: 'settings',
        name: 'Settings',
        component: () => import('@/pages/settings/index.vue')
      }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// 路由导航守卫
router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore()

  // 匿名页（登录 / 注册）直接放行
  if (to.meta?.anonymous) {
    next()
    return
  }

  // 未登录 → 跳登录页（带上原目标，登录后可回跳）
  if (!authStore.isAuthenticated()) {
    next({ path: '/login', query: to.fullPath === '/' ? undefined : { redirect: to.fullPath } })
    return
  }

  next()
})

export default router
