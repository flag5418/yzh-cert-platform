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
      // ===== 系统管理 =====
      { path: 'system/organization', name: 'SystemOrganization', component: () => import('@/pages/system/organization/index.vue') },
      { path: 'system/role-user', name: 'SystemRoleUser', component: () => import('@/pages/system/role-user/index.vue') },
      { path: 'system/role-menu', name: 'SystemRoleMenu', component: () => import('@/pages/system/role-menu/index.vue') },
      { path: 'system/role-api', name: 'SystemRoleApi', component: () => import('@/pages/system/role-api/index.vue') },
      { path: 'system/menu', name: 'SystemMenu', component: () => import('@/pages/system/menu/index.vue') },
      { path: 'system/api', name: 'SystemApi', component: () => import('@/pages/system/api/index.vue') },
      { path: 'system/dictionary', name: 'SystemDictionary', component: () => import('@/pages/system/dictionary/index.vue') },
      { path: 'system/log', name: 'SystemLog', component: () => import('@/pages/system/log/index.vue') },
      { path: 'system/config', name: 'SystemConfig', component: () => import('@/pages/system/config/index.vue') },

      // ===== 业务管理 =====
{ path: 'cert/iso-standard', name: 'CertIsoStandard', component: () => import('@/pages/foundation/iso-standard/index.vue') },
{ path: 'cert/cert-body', name: 'CertCertificationBody', component: () => import('@/pages/foundation/certification-body/index.vue') },
{ path: 'foundation/phase-definition', name: 'FoundationPhaseDefinition', component: () => import('@/pages/foundation/phase-definition/index.vue') },
      { path: 'cert/link-org-standard', name: 'CertLinkOrgStandard', component: () => import('@/pages/workflow/link-org-standard/index.vue') },
      { path: 'cert/link-org-stage', name: 'CertLinkOrgStage', component: () => import('@/pages/workflow/link-org-stage/index.vue') },
      { path: 'business/directory-manager', name: 'BusinessDirectoryManager', component: () => import('@/pages/workflow/directory/index.vue') },
      { path: 'business/doc-extraction-rule', name: 'BusinessDocExtractionRule', component: () => import('@/pages/workflow/doc-extraction/index.vue') },
      { path: 'business/report-def', name: 'BusinessReportDef', component: () => import('@/pages/workflow/report-rule/index.vue') },
      { path: 'business/prompt-template', name: 'BusinessPromptTemplate', component: () => import('@/pages/workflow/prompt-template/index.vue') },
      { path: 'business/skill-manage', name: 'BusinessSkillManage', component: () => import('@/pages/workflow/job-skill/index.vue') },
      { path: 'business/nc-config', name: 'BusinessNcConfig', component: () => import('@/pages/workflow/nc-config/index.vue') },
      { path: 'business/workflow-rules', name: 'BusinessWorkflowRules', component: () => import('@/pages/workflow/workflow-rules/index.vue') },
      { path: 'business/report-rule-config', name: 'BusinessReportRuleConfig', component: () => import('@/pages/workflow/report-rule-config/index.vue') },
      { path: 'business/ai-usage', name: 'BusinessAiUsage', component: () => import('@/pages/workflow/ai-usage/index.vue') },
      { path: 'business/queue-monitor', name: 'BusinessQueueMonitor', component: () => import('@/pages/workflow/queue/index.vue') }
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
