import { createRouter, createWebHistory, createWebHashHistory } from 'vue-router'
import adminViewGird from './viewGird.admin.js'
import auditorViewGird from './viewGird.auditor.js'
import store from '../store/index'
import redirect from './redirect'

/**
 * 路由结构说明（V1.0 - Phase 1 实施）：
 * 
 * /                     → 管理员主空间（原 Index.vue 布局）
 * /cert/auditor           → 审核员主空间
 * /login                → 管理员登录（兼容旧版）
 * /admin-login          → 管理员登录（新版命名）
 * /auditor-login        → 审核员登录
 */

// 角色 ID 常量（与数据库 Sys_Role.Role_Id 一致）
export const ROLE_IDS = {
  SUPER_ADMIN: 1,                    // 超级管理员
  MAINTAINER: 10,                    // 系统维护人员
  AUDITOR_ADMIN: 200,                // 审核端管理员
  AUDITOR: 20                        // 基础审核员
}

// 系统管理模块可访问的角色 ID
export const ADMIN_ROLE_IDS = [ROLE_IDS.SUPER_ADMIN, ROLE_IDS.MAINTAINER]

// 审核端模块可访问的角色 ID
export const AUDITOR_ROLE_IDS = [ROLE_IDS.AUDITOR_ADMIN, ROLE_IDS.AUDITOR]

// 判断是否为管理员角色
export function isAdminRole(roleNameOrId) {
  // 支持通过角色名或角色ID判断
  if (typeof roleNameOrId === 'number') {
    return ADMIN_ROLE_IDS.includes(roleNameOrId)
  }
  // 如果是角色名，也检查常见名称
  const nameStr = String(roleNameOrId || '')
  return nameStr.includes('管理员') || nameStr.includes('维护') || ADMIN_ROLE_IDS.includes(roleNameOrId)
}

// 判断是否为审核员角色
export function isAuditorRole(roleNameOrId) {
  // 支持通过角色名或角色ID判断
  if (typeof roleNameOrId === 'number') {
    return AUDITOR_ROLE_IDS.includes(roleNameOrId)
  }
  // 如果是角色名，也检查常见名称
  const nameStr = String(roleNameOrId || '')
  return nameStr.includes('审核') || AUDITOR_ROLE_IDS.includes(roleNameOrId)
}

const routes = [
  // ==================== 管理员主空间 ====================
  {
    path: '/',
    name: 'Admin',
    component: () => import('@/views/Index.vue'),  // Phase 3 迁移后改为 @/views/admin/Index.vue
    redirect: '/home',
    meta: { role: 'admin' },
    children: [
      ...adminViewGird,
      ...redirect,
      {
        path: '/home',
        name: 'home',
        component: () => import('@/views/Home.vue')  // Phase 3 迁移后改为 @/views/admin/Home.vue
      }, {
        path: '/UserInfo',
        name: 'UserInfo',
        component: () => import('@/views/cert/admin/system/UserInfo.vue')
      },
      {
        path: '/sysMenu',
        name: 'sysMenu',
        component: () => import('@/pages/system/menu/index.vue')
      }
    ]
  },

  // ==================== 审核员主空间 ====================
  // 审核员登录后直接跳转到工作台（Workspace.vue 是完整页面，非布局容器）
  {
    path: '/auditor',
    redirect: '/auditor/workspace'
  },
  // 审核员工作台（独立页面）
  {
    path: '/auditor/workspace',
    name: 'AuditorWorkspace',
    component: () => import('@/views/cert/auditor/Workspace.vue'),
    meta: { role: 'auditor' }
  },
  
  // ==================== 登录页 ====================
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/Login.vue'),  // Phase 3 迁移后改为 @/views/admin/Login.vue
    meta:{
      anonymous:true
    }
  },
  {
    path: '/admin-login',
    name: 'AdminLogin',
    component: () => import('@/views/Login.vue'),  // 复用管理员登录组件
    meta:{
      anonymous:true
    }
  },
  // 审核员登录/注册页
  {
    path: '/auditor-login',
    name: 'AuditorLogin',
    component: () => import('@/views/cert/auditor/Login.vue'),
    meta:{
      anonymous:true
    }
  }
];

const router = createRouter({
  history: createWebHashHistory(), //createWebHistory(process.env.BASE_URL),
  routes
})


router.beforeEach((to, from, next) => {
  if (to.matched.length == 0) return next({ path: '/404' });
  
  // 加载提示
  store.dispatch("onLoading", true);
  
  // 公开页面直接放行
  if (to.hasOwnProperty('meta') && to.meta.anonymous) {
    return next();
  }
  
  // 未登录跳转到登录页
  if (!store.getters.isLogin()) {
    return next({ path: '/login', query: { redirect: Math.random() } });
  }
  
  // 已登录，获取用户角色信息
  const userInfo = store.getters.getUserInfo()
  const roleId = userInfo?.roleId
  const roleName = userInfo?.roleName || ''
  
  // 管理员路由：只有管理员角色可访问
  if (to.path.startsWith('/CertPlatform') || to.path.startsWith('/Sys_') || to.path === '/home' || to.path === '/') {
    if (!isAdminRole(roleId) && !isAdminRole(roleName)) {
      // 审核员尝试访问管理员路由 → 重定向到审核端
      if (isAuditorRole(roleId) || isAuditorRole(roleName)) {
        return next('/cert/auditor/workspace');
      }
      // 其他角色 → 跳转到登录页
      return next('/login');
    }
  }
  
  // 审核员路由：只有审核端角色可访问
  if (to.path.startsWith('/cert/auditor') || to.path.startsWith('/auditor')) {
    if (!isAuditorRole(roleId) && !isAuditorRole(roleName)) {
      // 管理员尝试访问审核员路由 → 重定向到管理员首页
      if (isAdminRole(roleId) || isAdminRole(roleName)) {
        return next('/home');
      }
      // 其他角色 → 跳转到登录页
      return next('/login');
    }
  }
  
  return next();
})
router.afterEach((to, from) => {
  store.dispatch("onLoading", false);
})
router.onError((error) => {
  // const targetPath = router.currentRoute.value.matched;
  try {
      console.log(error.message);
    if (process.env.NODE_ENV == 'development') {
      //alert(error.message)
    }
    localStorage.setItem("route_error", error.message)
  } catch (e) {

  }
 // window.location.href = '/'
});
export default router
