import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'

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
    redirect: '/system/user',
    children: [
      // 系统管理
      { path: 'system/user', name: 'SystemUser', component: () => import('@/pages/system/user/index.vue') },
      { path: 'system/role', name: 'SystemRole', component: () => import('@/pages/system/role/index.vue') },
      { path: 'system/dept', name: 'SystemDept', component: () => import('@/pages/system/dept/index.vue') },
      { path: 'system/dict', name: 'SystemDict', component: () => import('@/pages/system/dict/index.vue') },
      { path: 'system/menu', name: 'SystemMenu', component: () => import('@/pages/system/menu/index.vue') },
      { path: 'system/log', name: 'SystemLog', component: () => import('@/pages/system/log/index.vue') },
      // 认证业务
      { path: 'cert/iso-standard', name: 'CertIsoStandard', component: () => import('@/pages/cert/iso-standard/index.vue') },
      { path: 'cert/iso-clause', name: 'CertIsoClause', component: () => import('@/pages/cert/iso-clause/index.vue') },
      { path: 'cert/cert-stage', name: 'CertStage', component: () => import('@/pages/cert/cert-stage/index.vue') },
      { path: 'cert/cert-body', name: 'CertBody', component: () => import('@/pages/cert/cert-body/index.vue') },
      { path: 'cert/enterprise', name: 'CertEnterprise', component: () => import('@/pages/cert/enterprise/index.vue') },
      // 业务管理
      { path: 'business/prompt-template', name: 'PromptTemplate', component: () => import('@/pages/business/prompt-template/index.vue') },
      { path: 'business/skill-manage', name: 'SkillManage', component: () => import('@/pages/business/skill-manage/index.vue') },
      { path: 'business/ai-usage', name: 'AIUsageMonitor', component: () => import('@/pages/business/ai-usage-monitor/index.vue') },
      { path: 'business/directory-manager', name: 'DirectoryManager', component: () => import('@/pages/business/directory-manager/index.vue') },
      { path: 'business/doc-extraction-rule', name: 'DocExtractionRule', component: () => import('@/pages/business/doc-extraction-rule/index.vue') },
      { path: 'business/nc-config', name: 'NCConfig', component: () => import('@/pages/business/nc-config/index.vue') },
      { path: 'business/report-rule-config', name: 'ReportRuleConfig', component: () => import('@/pages/business/report-rule-config/index.vue') },
      { path: 'business/sys-config-manager', name: 'SysConfigManager', component: () => import('@/pages/business/sys-config-manager/index.vue') }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router
