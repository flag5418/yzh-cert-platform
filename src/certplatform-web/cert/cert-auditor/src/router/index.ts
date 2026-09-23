import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/store/auth'

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
