import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { yzhLoginRoute, yzhSystemRoutes } from '@yzh-core/router'
import { useAuthStore } from '@/store/auth'
import { useMenuStore } from '@/store/menu'
import CertAdminLayout from '@/layouts/CertAdminLayout.vue'

/**
 * 业务路由（foundation / workflow 等宿主页面）
 *
 * ⚠️ 每条 path 必须与 `Sys_Menu.Url` **逐字一致** —— 由守卫 **R12** 双向校验：
 *    菜单有路由无 = ⛔ 报错（点击必白屏）；路由有菜单无 = ⛔ 报错（孤儿路由）。
 * ⛔ 2026-09-24 移除 `foundation/phase-definition` 与 `business/job-skill`：
 *    `Sys_Menu` 中无对应菜单 → 属「孤儿路由」（只能手输 URL 到达）。
 *    功能已由现有后台管理模块覆盖，不再单独提供页面。
 */
const businessRoutes: RouteRecordRaw[] = [
  // ★ 标准管理（MENU_00219）—— 体系 → 族 → 版本 三层只读树 + 族 CRUD
  { path: 'cert/standard-manage', name: 'CertStandardManage', component: () => import('@/pages/foundation/standard-manage/index.vue') },
  { path: 'cert/iso-standard', name: 'CertIsoStandard', component: () => import('@/pages/foundation/iso-standard/index.vue') },
  { path: 'foundation/certification-body', name: 'FoundationCertificationBody', component: () => import('@/pages/foundation/certification-body/index.vue') },
  { path: 'cert/cert-stage', name: 'CertCertStage', component: () => import('@/pages/foundation/cert-stage/index.vue') },
  { path: 'cert/link-org-standard', name: 'CertLinkOrgStandard', component: () => import('@/pages/foundation/cert-org-standard/index.vue') },
  { path: 'cert/link-org-stage', name: 'CertLinkOrgStage', component: () => import('@/pages/foundation/cert-org-stage/index.vue') },
  { path: 'business/directory-manager', name: 'BusinessDirectoryManager', component: () => import('@/pages/workflow/directory/index.vue') },
  // ★ 体系认证全局参数定义（MENU_00217）—— 按「机构 × 标准 × 阶段」预定义填充参数
  { path: 'business/fill-param-def', name: 'BusinessFillParamDef', component: () => import('@/pages/cert/fill-param-def/index.vue') },
  { path: 'business/doc-extraction-rule', name: 'BusinessDocExtractionRule', component: () => import('@/pages/workflow/doc-extraction-rule/index.vue') },
  // ★ 标准文档填写规则（MENU_00218）—— 给空白模板标注锚点 + 挂全文填写提示词
  { path: 'business/doc-fill-rule', name: 'BusinessDocFillRule', component: () => import('@/pages/workflow/doc-fill-rule/index.vue') },
  { path: 'business/report-def', name: 'BusinessReportDef', component: () => import('@/pages/workflow/report-rule/index.vue') },
  { path: 'business/prompt-template', name: 'BusinessPromptTemplate', component: () => import('@/pages/workflow/prompt-template/index.vue') },
  { path: 'business/skill-manage', name: 'BusinessSkillManage', component: () => import('@/pages/workflow/skill-manage/index.vue') },
  { path: 'business/nc-config', name: 'BusinessNcConfig', component: () => import('@/pages/workflow/nc-config/designer.vue') },
  { path: 'business/workflow-rules', name: 'BusinessWorkflowRules', component: () => import('@/pages/workflow/nc-config/index.vue') },
  { path: 'business/report-rule-config', name: 'BusinessReportRuleConfig', component: () => import('@/pages/workflow/report-rule-config/index.vue') },
  { path: 'business/ai-usage', name: 'BusinessAiUsage', component: () => import('@/pages/workflow/ai-usage/index.vue') },
  { path: 'business/queue-monitor', name: 'BusinessQueueMonitor', component: () => import('@/pages/workflow/queue/index.vue') }
]

/**
 * 路由表 —— ★ 手动装配（S6，2026-10-10）
 *
 * 使用 CertAdminLayout 作为应用壳（内置 MessageBell 铃铛通知），
 * 替代 createYzhRoutes() 硬编码的 YzhAppLayout。
 * `...yzhSystemRoutes` 由本文件内部装配，**勿在此重复注册同 path**
 * （同 path 双注册 → 后注册静默失效）。
 *
 * ⚠️ 与旧手写版本的差异（改前已确认**全仓零处**按 name 导航，均为 path 形式）：
 *   - shell 的 `name`：`Home` → `YzhShell`
 *   - 登录页的 `name`：`Login` → `YzhLogin`
 */
const routes: RouteRecordRaw[] = [
  yzhLoginRoute,
  {
    path: '/',
    name: 'YzhShell',
    component: CertAdminLayout,
    props: { menuTag: 'admin', logoText: 'YZH', appTitle: '映智汇认证平台' },
    redirect: '/system/organization',
    children: [
      ...yzhSystemRoutes,
      ...businessRoutes,
    ],
  },
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
