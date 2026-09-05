/**
 * 路由配置（统一入口）
 * 注意：viewGird.admin.js 是旧版过渡文件，已被此文件取代
 */

// 系统管理页面路由（V4 新架构）
const systemRoutes = [
  {
    path: '/Sys_User',
    name: 'Sys_User',
    component: () => import('@/pages/system/user/index.vue')
  },
  {
    path: '/permission',
    name: 'permission',
    component: () => import('@/views/cert/admin/system/Permission.vue')
  },
  {
    path: '/Sys_Dictionary',
    name: 'Sys_Dictionary',
    component: () => import('@/pages/system/dict/index.vue')
  },
  {
    path: '/Sys_Dictionary/List',
    name: 'Sys_Dictionary_List',
    component: () => import('@/pages/system/dict/list.vue')
  },
  {
    path: '/Sys_Role',
    name: 'Sys_Role',
    component: () => import('@/pages/system/role/index.vue')
  },
  {
    path: '/Sys_Department',
    name: 'Sys_Department',
    component: () => import('@/pages/system/dept/index.vue')
  },
  {
    path: '/Sys_Menu',
    name: 'Sys_Menu',
    component: () => import('@/pages/system/menu/index.vue')
  },
  {
    path: '/Sys_Log',
    name: 'Sys_Log',
    component: () => import('@/pages/system/log/index.vue')
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
  }
]

// 业务模块路由
const businessRoutes = [
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
  {
    path: '/CertPlatform/Standard/DirectoryConfig',
    name: 'DirectoryConfig',
    component: () => import('@/views/cert/admin/business/Standard/DirectoryManager/index.vue'),
    meta: { title: '标准文件管理' }
  },
  {
    path: '/CertPlatform/DocExtractionRule',
    name: 'DocExtractionRule',
    component: () => import('@/views/cert/admin/business/Standard/DocExtractionRule/index.vue'),
    meta: { title: '文档提取规则' }
  },
  {
    path: '/CertPlatform/SysConfig',
    name: 'SysConfig',
    component: () => import('@/views/cert/admin/business/Standard/SysConfigManager/index.vue'),
    meta: { title: '系统参数配置' }
  },
  {
    path: '/CertPlatform/ConvertQueueMonitor',
    name: 'ConvertQueueMonitor',
    component: () => import('@/yzh/views/QueueMonitor/index.vue'),
    meta: { title: '队列监控' }
  },
  {
    path: '/CertPlatform/PromptTemplate',
    name: 'PromptTemplate',
    component: () => import('@/views/cert/admin/business/Standard/PromptTemplate/index.vue'),
    meta: { title: 'Prompt 模板管理' }
  },
  {
    path: '/CertPlatform/AIUsageMonitor',
    name: 'AIUsageMonitor',
    component: () => import('@/views/cert/admin/business/Standard/AIUsageMonitor/index.vue'),
    meta: { title: 'AI 费用监控' }
  },
  {
    path: '/CertPlatform/ISOClause',
    name: 'ISOClause',
    component: () => import('@/views/cert/admin/business/Standard/ISOClause.vue'),
    meta: { title: '标准条款管理' }
  },
  {
    path: '/CertPlatform/WorkflowRules/Rules',
    name: 'ValidationRuleList',
    component: () => import('@/views/cert/admin/business/Standard/WorkflowRules/List.vue'),
    meta: { title: 'NC 检查规则' }
  },
  {
    path: '/CertPlatform/WorkflowRules/ReportDef',
    name: 'ReportDefinition',
    component: () => import('@/views/cert/admin/business/Standard/WorkflowRules/ReportDefinition.vue'),
    meta: { title: '报告章节定义' }
  },
  {
    path: '/CertPlatform/SkillManage',
    name: 'SkillManage',
    component: () => import('@/views/cert/admin/business/Standard/SkillManage/index.vue'),
    meta: { title: 'Skill 管理' }
  },
  {
    path: '/CertPlatform/NCConfig',
    name: 'NCConfig',
    component: () => import('@/views/cert/admin/business/Standard/NCConfig/index.vue'),
    meta: { title: 'NC 规则配置' }
  },
  {
    path: '/CertPlatform/ReportRuleConfig',
    name: 'ReportRuleConfig',
    component: () => import('@/views/cert/admin/business/Standard/ReportRuleConfig/index.vue'),
    meta: { title: '报告规则配置' }
  },
  // ====== V4 业务基础数据维护（彻底脱离 view-grid） ======
  {
    path: '/Business/ISOStandard',
    name: 'Business_ISOStandard',
    component: () => import('@/pages/business/iso-standard/index.vue'),
    meta: { title: 'ISO 标准注册' }
  },
  {
    path: '/Business/ISOClause',
    name: 'Business_ISOClause',
    component: () => import('@/pages/business/iso-clause/index.vue'),
    meta: { title: 'ISO 条款' }
  },
  {
    path: '/Business/CertStage',
    name: 'Business_CertStage',
    component: () => import('@/pages/business/cert-stage/index.vue'),
    meta: { title: '认证阶段' }
  },
  {
    path: '/Business/CertBody',
    name: 'Business_CertBody',
    component: () => import('@/pages/business/cert-body/index.vue'),
    meta: { title: '认证机构' }
  },
  {
    path: '/Business/Enterprise',
    name: 'Business_Enterprise',
    component: () => import('@/pages/business/enterprise/index.vue'),
    meta: { title: '企业管理' }
  }
]

let viewgird = [...systemRoutes, ...businessRoutes]

export default viewgird
