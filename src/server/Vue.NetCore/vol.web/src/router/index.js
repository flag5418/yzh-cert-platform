import { createRouter, createWebHistory, createWebHashHistory } from 'vue-router'
import adminViewGird from './viewGird.admin.js'
import auditorViewGird from './viewGird.auditor.js'
import store from '../store/index'
import redirect from './redirect'

/**
 * 路由结构说明（V1.0 - Phase 1 实施）：
 * 
 * /                     → 管理员主空间（原 Index.vue 布局）
 * /auditor              → 审核员主空间（Phase 2 启用）
 * /login                → 管理员登录（兼容旧版）
 * /admin-login          → 管理员登录（新版命名）
 * /auditor-login        → 审核员登录（Phase 2 启用）
 */

// 审核员角色 ID（与数据库 Sys_Role 一致）
export const AUDITOR_ROLE_ID = 100

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
        component: () => import('@/views/sys/UserInfo.vue')
      },
      {
        path: '/sysMenu',
        name: 'sysMenu',
        component: () => import('@/views/sys/system/Sys_Menu.vue')
      }, {
        path: '/coder',
        name: 'coder',
        component: () => import('@/views/builder/coder.vue')
      },
      {
        path: '/formDraggable',  //表单设计
        name: 'formDraggable',
        component: () => import('@/views/formDraggable/formDraggable.vue')
      },
      {
        path: '/formSubmit',  //表单提交页面
        name: 'formSubmit',
        component: () => import('@/views/formDraggable/FormSubmit.vue'),
        meta:{
          keepAlive:false
        }
      },
      {
        path: '/formCollectionResultTree',  //显示收集的数据表单
        name: 'formCollectionResultTree',
        component: () => import('@/views/formDraggable/FormCollectionResultTree.vue'),
        meta:{
          keepAlive:false
        }
      },
      {
        path: '/signalR',  //消息推送
        name: 'signalR',
        component: () => import('@/views/signalR/Index.vue'),
        meta:{
          keepAlive:false
        }
      }
    ]
  },

  // ==================== 审核员主空间（Phase 2 启用） ====================
  {
    path: '/auditor',
    name: 'Auditor',
    component: () => import('@/views/auditor/Workspace.vue'),
    meta: { role: 'auditor' },
    children: [
      ...auditorViewGird
    ]
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
    component: () => import('@/views/auditor/Login.vue'),
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
  //2020.06.03增加路由切换时加载提示
  store.dispatch("onLoading", true);
  if ((to.hasOwnProperty('meta') && to.meta.anonymous) || store.getters.isLogin() || to.path == '/login') {
    return next();
  }

  next({ path: '/login', query: { redirect: Math.random() } });
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
