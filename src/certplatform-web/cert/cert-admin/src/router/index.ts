import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/store/auth'
import { useMenuStore } from '@/store/menu'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/layouts/AdminLogin.vue')
  },
  {
    path: '/',
    name: 'Home',
    component: () => import('@/layouts/AdminLayout.vue'),
    redirect: '/system/organization',
    children: [
      // ===== System 域 - 系统管理 =====
      { path: 'system/organization', name: 'SystemOrganization', component: () => import('@/pages/system/organization/index.vue') },
      { path: 'system/role', name: 'SystemRole', component: () => import('@/pages/system/role/index.vue') },
      { path: 'system/dictionary', name: 'SystemDictionary', component: () => import('@/pages/system/dictionary/index.vue') },
      { path: 'system/menu', name: 'SystemMenu', component: () => import('@/pages/system/menu/index.vue') },
      { path: 'system/dept', name: 'SystemDept', component: () => import('@/pages/system/dept/index.vue') },
      { path: 'system/log', name: 'SystemLog', component: () => import('@/pages/system/log/index.vue') },

      // ===== Foundation 域 - ISO 标准体系主数据 =====
      { path: 'foundation/iso-standard', name: 'FoundationIsoStandard', component: () => import('@/pages/foundation/iso-standard/index.vue') },
      { path: 'foundation/iso-clause', name: 'FoundationIsoClause', component: () => import('@/pages/foundation/iso-clause/index.vue') },
      { path: 'foundation/certification-body', name: 'FoundationCertificationBody', component: () => import('@/pages/foundation/certification-body/index.vue') },
      { path: 'foundation/cert-stage', name: 'FoundationCertStage', component: () => import('@/pages/foundation/cert-stage/index.vue') },

      // ===== Workflow 域 - 审核流程与业务管理 =====
      { path: 'workflow/enterprise', name: 'WorkflowEnterprise', component: () => import('@/pages/workflow/enterprise/index.vue') },
      { path: 'workflow/workflow', name: 'WorkflowDefinition', component: () => import('@/pages/workflow/workflow/list.vue') },
      { path: 'workflow/prompt-template', name: 'WorkflowPromptTemplate', component: () => import('@/pages/workflow/prompt-template/index.vue') },
      { path: 'workflow/nc-config', name: 'WorkflowNcConfig', component: () => import('@/pages/workflow/nc-config/index.vue') },
      { path: 'workflow/report-rule', name: 'WorkflowReportRule', component: () => import('@/pages/workflow/report-rule/index.vue') },
      { path: 'workflow/job-skill', name: 'WorkflowJobSkill', component: () => import('@/pages/workflow/job-skill/index.vue') },
      { path: 'workflow/doc-extraction', name: 'WorkflowDocExtraction', component: () => import('@/pages/workflow/doc-extraction/index.vue') },
      { path: 'workflow/directory', name: 'WorkflowDirectory', component: () => import('@/pages/workflow/directory/index.vue') },
      { path: 'workflow/queue', name: 'WorkflowQueue', component: () => import('@/pages/workflow/queue/index.vue') },
      { path: 'workflow/ai-usage', name: 'WorkflowAiUsage', component: () => import('@/pages/workflow/ai-usage/index.vue') },
      { path: 'workflow/file-upload', name: 'WorkflowFileUpload', component: () => import('@/pages/workflow/file-upload/index.vue') },
      { path: 'workflow/sys-config', name: 'WorkflowSysConfig', component: () => import('@/pages/workflow/sys-config/index.vue') }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// 路由导航守卫
router.beforeEach(async (to, _from, next) => {
  const authStore = useAuthStore()
  const menuStore = useMenuStore()

  // 登录页直接放行
  if (to.path === '/login') {
    next()
    return
  }

  // 检查是否已登录
  if (authStore.isAuthenticated()) {
    // 如果菜单未加载，异步加载菜单
    if (!menuStore.loaded) {
      await menuStore.loadMenus()
    }
    next()
  } else {
    // 未登录，跳转到登录页
    next('/login')
  }
})

export default router
