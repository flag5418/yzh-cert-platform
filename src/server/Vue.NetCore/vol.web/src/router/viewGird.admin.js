/**
 * 管理员端路由配置
 * 来源：原 viewGird.js 全量迁移
 * 
 * 注意：这是 Phase 1 准备阶段的过渡文件。
 * Phase 3 迁移时，路径需批量替换为 @/views/admin/ 前缀
 */

//多个页面指向同一个菜单时请加上属性：
// meta: {
//   dynamic: true,
// }
let viewgird = [
  {
    path: '/Sys_Log',
    name: 'sys_Log',
    component: () => import('@/views/cert/admin/system/system/Sys_Log.vue')
  },
  {
    path: '/Sys_User',
    name: 'Sys_User',
    component: () => import('@/views/cert/admin/system/system/Sys_User.vue')
  },
  {
    path: '/permission',
    name: 'permission',
    component: () => import('@/views/cert/admin/system/Permission.vue')
  },

  {
    path: '/Sys_Dictionary',
    name: 'Sys_Dictionary',
    component: () => import('@/views/cert/admin/system/system/Sys_Dictionary.vue')
  },
  {
    path: '/Sys_Role',
    name: 'Sys_Role',
    component: () => import('@/views/cert/admin/system/system/Sys_Role.vue')
  },
  {
    path: '/Sys_Department',
    name: 'Sys_Department',
    component: () => import('@/views/cert/admin/system/system/Sys_Department.vue')
  },
  {
    path: '/Sys_QuartzOptions',
    name: 'Sys_QuartzOptions',
    component: () => import('@/views/cert/admin/system/quartz/Sys_QuartzOptions.vue')
  },
  {
    path: '/Sys_QuartzLog',
    name: 'Sys_QuartzLog',
    component: () => import('@/views/cert/admin/system/quartz/Sys_QuartzLog.vue')
  },
  {
    path: '/Sys_WorkFlow',
    name: 'Sys_WorkFlow',
    component: () => import('@/views/cert/admin/system/flow/Sys_WorkFlow.vue')
  },
  {
    path: '/Sys_WorkFlowTable',
    name: 'Sys_WorkFlowTable',
    component: () => import('@/views/cert/admin/system/flow/Sys_WorkFlowTable.vue')
  },
  // ==================== CertPlatform 模块路由 ====================
  {
    path: '/CertPlatform/Cert/CertificationBody',
    name: 'CertificationBody',
    component: () => import('@/views/cert/admin/business/CertificationBody/CertificationBody.vue'),
    meta: { title: '认证机构管理' }
  },
  {
    path: '/CertPlatform/Base/ISOStandard',
    name: 'BaseISOStandard',
    component: () => import('@/views/cert/admin/business/Base/ISOStandard/ISOStandard.vue'),
    meta: { title: 'ISO标准注册' }
  },
  {
    path: '/CertPlatform/Base/CertStage',
    name: 'CertStage',
    component: () => import('@/views/cert/admin/business/Base/CertStage/CertStage.vue'),
    meta: { title: '认证阶段定义' }
  },
  {
    path: '/CertPlatform/Link/OrgStandard',
    name: 'OrgStandard',
    component: () => import('@/views/cert/admin/business/Link/OrgStandard/OrgStandard.vue'),
    meta: { title: '机构-标准关联' }
  },
  {
    path: '/CertPlatform/Link/OrgStage',
    name: 'OrgStage',
    component: () => import('@/views/cert/admin/business/Link/OrgStage/OrgStage.vue'),
    meta: { title: '机构-阶段关联' }
  },
  
  // 标准文件管理（扁平路由，菜单 URL 直接匹配）
  {
    path: '/CertPlatform/Standard/DirectoryConfig',
    name: 'DirectoryConfig',
    component: () => import('@/views/cert/admin/business/Standard/DirectoryManager/index.vue'),
    meta: { title: '标准文件管理' }
  },
  // 文档提取规则管理（独立菜单）
  {
    path: '/CertPlatform/DocExtractionRule',
    name: 'DocExtractionRule',
    component: () => import('@/views/cert/admin/business/Standard/DocExtractionRule/index.vue'),
    meta: { title: '文档提取规则' }
  },
  // 系统参数配置
  {
    path: '/CertPlatform/SysConfig',
    name: 'SysConfig',
    component: () => import('@/views/cert/admin/business/Standard/SysConfigManager/index.vue'),
    meta: { title: '系统参数配置' }
  },
  // 队列监控（yzh 队列中心通用页面）
  {
    path: '/CertPlatform/ConvertQueueMonitor',
    name: 'ConvertQueueMonitor',
    component: () => import('@/yzh/views/QueueMonitor/index.vue'),
    meta: { title: '队列监控' }
  },
  // Prompt 模板管理
  {
    path: '/CertPlatform/PromptTemplate',
    name: 'PromptTemplate',
    component: () => import('@/views/cert/admin/business/Standard/PromptTemplate/index.vue'),
    meta: { title: 'Prompt 模板管理' }
  },
  // AI 费用监控
  {
    path: '/CertPlatform/AIUsageMonitor',
    name: 'AIUsageMonitor',
    component: () => import('@/views/cert/admin/business/Standard/AIUsageMonitor/index.vue'),
    meta: { title: 'AI 费用监控' }
  },
  // 标准条款管理
  {
    path: '/CertPlatform/ISOClause',
    name: 'ISOClause',
    component: () => import('@/views/cert/admin/business/Standard/ISOClause.vue'),
    meta: { title: '标准条款管理' }
  },
  // NC 检查规则（扁平路由）
  {
    path: '/CertPlatform/WorkflowRules/Rules',
    name: 'ValidationRuleList',
    component: () => import('@/views/cert/admin/business/Standard/WorkflowRules/List.vue'),
    meta: { title: 'NC 检查规则' }
  },
  // 报告章节定义（扁平路由）
  {
    path: '/CertPlatform/WorkflowRules/ReportDef',
    name: 'ReportDefinition',
    component: () => import('@/views/cert/admin/business/Standard/WorkflowRules/ReportDefinition.vue'),
    meta: { title: '报告章节定义' }
  },
  // Skill 管理（工作流节点技能配置：输入/输出/反射/API）
  {
    path: '/CertPlatform/SkillManage',
    name: 'SkillManage',
    component: () => import('@/views/cert/admin/business/Standard/SkillManage/index.vue'),
    meta: { title: 'Skill 管理' }
  },
  // NC 规则配置（三栏：机构树 + NC 检查项 + 工作流画布，独立菜单）
  {
    path: '/CertPlatform/NCConfig',
    name: 'NCConfig',
    component: () => import('@/views/cert/admin/business/Standard/NCConfig/index.vue'),
    meta: { title: 'NC 规则配置' }
  },
  // 报告规则配置（三栏：机构树 + 报告章节 + 工作流画布，独立菜单）
  {
    path: '/CertPlatform/ReportRuleConfig',
    name: 'ReportRuleConfig',
    component: () => import('@/views/cert/admin/business/Standard/ReportRuleConfig/index.vue'),
    meta: { title: '报告规则配置' }
  }
]

export default viewgird
