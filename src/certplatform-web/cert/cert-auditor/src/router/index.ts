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
      // ★ 企业全局参数定义（MENU_AUD_10）—— 企业档案 × 后台参数定义的关联列表 + 填充预览
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
      {
        path: 'organization',
        name: 'Organization',
        component: () => import('@/pages/organization/index.vue')
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
