import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      redirect: '/login'
    },
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/login/Index.vue'),
      meta: { title: '登录', noAuth: true }
    },
    {
      path: '/workspace',
      name: 'Workspace',
      component: () => import('@/views/workspace/Index.vue'),
      meta: { title: '审核工作台' }
    },
    {
      path: '/audit/:taskId',
      name: 'AuditDetail',
      component: () => import('@/views/audit/Index.vue'),
      meta: { title: '审核详情' }
    },
    {
      path: '/report/:taskId',
      name: 'Report',
      component: () => import('@/views/report/Index.vue'),
      meta: { title: '审核报告' }
    }
  ]
})

const APP_TITLE = '审核员端'

// 全局守卫：未登录且非 noAuth 页面 → 跳转登录；登录后按 meta.title 设置 document.title
router.beforeEach((to) => {
  const token = localStorage.getItem('token')
  if (!token && !to.meta.noAuth) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }
  return true
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} - ${APP_TITLE}` : APP_TITLE
})

export default router
