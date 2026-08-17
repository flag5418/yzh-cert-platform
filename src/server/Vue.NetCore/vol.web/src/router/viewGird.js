//多个页面指向同一个菜单时请加上属性：
// meta: {
//   dynamic: true,
// }
let viewgird = [
  {
    path: '/Sys_Log',
    name: 'sys_Log',
    component: () => import('@/views/sys/system/Sys_Log.vue')
  },
  {
    path: '/Sys_User',
    name: 'Sys_User',
    component: () => import('@/views/sys/system/Sys_User.vue')
  },
  {
    path: '/permission',
    name: 'permission',
    component: () => import('@/views/sys/Permission.vue')
  },

  {
    path: '/Sys_Dictionary',
    name: 'Sys_Dictionary',
    component: () => import('@/views/sys/system/Sys_Dictionary.vue')
  },
  {
    path: '/Sys_Role',
    name: 'Sys_Role',
    component: () => import('@/views/sys/system/Sys_Role.vue')
  },

  // ==================== CertPlatform 模块路由 ====================
  {
    path: '/CertPlatform/Cert/CertificationBody',
    name: 'CertificationBody',
    component: () => import('@/views/cert/CertificationBody/CertificationBody.vue'),
    meta: { title: '认证机构管理' }
  },
  {
    path: '/CertPlatform/Base/ISOStandard',
    name: 'BaseISOStandard',
    component: () => import('@/views/cert/Base/ISOStandard/ISOStandard.vue'),
    meta: { title: 'ISO标准注册' }
  },
  {
    path: '/CertPlatform/Base/CertStage',
    name: 'CertStage',
    component: () => import('@/views/cert/Base/CertStage/CertStage.vue'),
    meta: { title: '认证阶段定义' }
  },
  {
    path: '/CertPlatform/Link/OrgStandard',
    name: 'OrgStandard',
    component: () => import('@/views/cert/Link/OrgStandard/OrgStandard.vue'),
    meta: { title: '机构-标准关联' }
  },
  {
    path: '/CertPlatform/Link/OrgStage',
    name: 'OrgStage',
    component: () => import('@/views/cert/Link/OrgStage/OrgStage.vue'),
    meta: { title: '机构-阶段关联' }
  },
  
  // 标准目录管理（基础配置子菜单）
  {
    path: '/CertPlatform/Standard',
    name: 'StandardDirectory',
    redirect: '/CertPlatform/Standard/DirectoryConfig',
    meta: { title: '标准目录管理' },
    children: [
      {
        path: 'DirectoryConfig',
        name: 'DirectoryConfig',
        component: () => import('@/views/cert/Standard/DirectoryManager/index.vue'),
        meta: { title: '目录管理' }
      }
    ]
  },
  // 文档提取规则管理（独立菜单）
  {
    path: '/CertPlatform/DocExtractionRule',
    name: 'DocExtractionRule',
    component: () => import('@/views/cert/Standard/DocExtractionRule/index.vue'),
    meta: { title: '文档提取规则' }
  },
  // 系统参数配置
  {
    path: '/CertPlatform/SysConfig',
    name: 'SysConfig',
    component: () => import('@/views/cert/Standard/SysConfigManager/index.vue'),
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
    component: () => import('@/views/cert/Standard/PromptTemplate/index.vue'),
    meta: { title: 'Prompt 模板管理' }
  },
  // AI 费用监控
  {
    path: '/CertPlatform/AIUsageMonitor',
    name: 'AIUsageMonitor',
    component: () => import('@/views/cert/Standard/AIUsageMonitor/index.vue'),
    meta: { title: 'AI 费用监控' }
  },
  // 标准条款管理
  {
    path: '/CertPlatform/ISOClause',
    name: 'ISOClause',
    component: () => import('@/views/cert/Standard/ISOClause.vue'),
    meta: { title: '标准条款管理' }
  },
  // 审核规则库
  {
    path: '/CertPlatform/WorkflowRules',
    name: 'WorkflowRules',
    redirect: '/CertPlatform/WorkflowRules/Rules',
    meta: { title: '审核规则库' },
    children: [
      { path: 'Rules', name: 'ValidationRuleList', component: () => import('@/views/cert/Standard/WorkflowRules/List.vue'), meta: { title: 'NC检查规则' } },
      { path: 'ReportDef', name: 'ReportDefinition', component: () => import('@/views/cert/Standard/WorkflowRules/ReportDefinition.vue'), meta: { title: '报告章节定义' } }
    ]
  },
  // 工作流设计器（独立页面，支持从规则/章节跳转）
  {
    path: '/CertPlatform/WorkflowDesigner',
    name: 'WorkflowDesigner',
    component: () => import('@/views/cert/Standard/WorkflowDesigner/Designer.vue'),
    meta: { title: '工作流设计器' }
  },
  // Skill 管理（工作流节点技能配置：输入/输出/反射/API）
  {
    path: '/CertPlatform/SkillManage',
    name: 'SkillManage',
    component: () => import('@/views/cert/Standard/SkillManage/index.vue'),
    meta: { title: 'Skill 管理' }
  },
  // NC 规则配置（三栏：机构树 + NC 检查项 + 工作流画布，独立菜单）
  {
    path: '/CertPlatform/NCConfig',
    name: 'NCConfig',
    component: () => import('@/views/cert/Standard/NCConfig/index.vue'),
    meta: { title: 'NC 规则配置' }
  }
]

//上面的demo、MES开头的都是示例菜单，可以任意删除 
export default viewgird