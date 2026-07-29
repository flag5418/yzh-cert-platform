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

export default router
