import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/layouts/AuditorLogin.vue')
  },
  {
    path: '/',
    name: 'Home',
    component: () => import('@/layouts/AuditorLayout.vue'),
    redirect: '/workspace',
    children: [
      {
        path: 'workspace',
        name: 'Workspace',
        component: () => import('@/pages/workspace/index.vue')
      }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router
