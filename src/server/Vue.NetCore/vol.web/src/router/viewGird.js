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
  {
    path: '/Sys_Department',
    name: 'Sys_Department',
    component: () => import('@/views/sys/system/Sys_Department.vue')
  },
  {
    path: '/Sys_QuartzOptions',
    name: 'Sys_QuartzOptions',
    component: () => import('@/views/sys/quartz/Sys_QuartzOptions.vue')
  },
  {
    path: '/Sys_QuartzLog',
    name: 'Sys_QuartzLog',
    component: () => import('@/views/sys/quartz/Sys_QuartzLog.vue')
  },
  {
    path: '/Sys_WorkFlow',
    name: 'Sys_WorkFlow',
    component: () => import('@/views/sys/flow/Sys_WorkFlow.vue')
  },
  {
    path: '/Sys_WorkFlowTable',
    name: 'Sys_WorkFlowTable',
    component: () => import('@/views/sys/flow/Sys_WorkFlowTable.vue')
  },
  {
    path: '/ProductionState',
    name: 'ProductionState',
    component: () => import('@/views/mes/state/ProductionState.vue')
  },

  // ==================== MES Demo 路由（保留参考，菜单已移到最后） ====================
  { path: '/MES_Customer', name: 'MES_Customer', component: () => import('@/views/mes/mes/MES_Customer.vue') },
  { path: '/MES_Supplier', name: 'MES_Supplier', component: () => import('@/views/mes/mes/MES_Supplier.vue') },
  { path: '/MES_ProductionLine', name: 'MES_ProductionLine', component: () => import('@/views/mes/mes/MES_ProductionLine.vue') },
  { path: '/MES_ProductionLineDevice', name: 'MES_ProductionLineDevice', component: () => import('@/views/mes/mes/MES_ProductionLineDevice.vue') },
  { path: '/MES_Material', name: 'MES_Material', component: () => import('@/views/mes/mes/MES_Material.vue') },
  { path: '/MES_MaterialCatalog', name: 'MES_MaterialCatalog', component: () => import('@/views/mes/mes/MES_MaterialCatalog.vue') },
  { path: '/MES_WarehouseManagement', name: 'MES_WarehouseManagement', component: () => import('@/views/mes/mes/MES_WarehouseManagement.vue') },
  { path: '/MES_LocationManagement', name: 'MES_LocationManagement', component: () => import('@/views/mes/mes/MES_LocationManagement.vue') },
  { path: '/MES_InventoryManagement', name: 'MES_InventoryManagement', component: () => import('@/views/mes/mes/MES_InventoryManagement.vue') },
  { path: '/MES_ProductInbound', name: 'MES_ProductInbound', component: () => import('@/views/mes/mes/MES_ProductInbound.vue') },
  { path: '/MES_ProductOutbound', name: 'MES_ProductOutbound', component: () => import('@/views/mes/mes/MES_ProductOutbound.vue') },
  { path: '/MES_EquipmentManagement', name: 'MES_EquipmentManagement', component: () => import('@/views/mes/mes/MES_EquipmentManagement.vue') },
  { path: '/MES_EquipmentRepair', name: 'MES_EquipmentRepair', component: () => import('@/views/mes/mes/MES_EquipmentRepair.vue') },
  { path: '/MES_EquipmentMaintenance', name: 'MES_EquipmentMaintenance', component: () => import('@/views/mes/mes/MES_EquipmentMaintenance.vue') },
  { path: '/MES_EquipmentFaultRecord', name: 'MES_EquipmentFaultRecord', component: () => import('@/views/mes/mes/MES_EquipmentFaultRecord.vue') },
  { path: '/MES_Process', name: 'MES_Process', component: () => import('@/views/mes/mes/MES_Process.vue') },
  { path: '/MES_ProcessRoute', name: 'MES_ProcessRoute', component: () => import('@/views/mes/mes/MES_ProcessRoute.vue') },
  { path: '/MES_ProcessReport', name: 'MES_ProcessReport', component: () => import('@/views/mes/mes/MES_ProcessReport.vue') },
  { path: '/MES_ProductionOrder', name: 'MES_ProductionOrder', component: () => import('@/views/mes/mes/MES_ProductionOrder.vue') },
  { path: '/MES_ProductionPlanDetail', name: 'MES_ProductionPlanDetail', component: () => import('@/views/mes/mes/MES_ProductionPlanDetail.vue') },
  { path: '/MES_ProductionPlanChangeRecord', name: 'MES_ProductionPlanChangeRecord', component: () => import('@/views/mes/mes/MES_ProductionPlanChangeRecord.vue') },
  { path: '/MES_ProductionReporting', name: 'MES_ProductionReporting', component: () => import('@/views/mes/mes/MES_ProductionReporting.vue') },
  { path: '/MES_ProductionReportingDetail', name: 'MES_ProductionReportingDetail', component: () => import('@/views/mes/mes/TestService.vue') },
  { path: '/MES_DefectiveProductRecord', name: 'MES_DefectiveProductRecord', component: () => import('@/views/mes/mes/MES_DefectiveProductRecord.vue') },
  { path: '/MES_QualityInspectionPlan', name: 'MES_QualityInspectionPlan', component: () => import('@/views/mes/mes/MES_QualityInspectionPlan.vue') },
  { path: '/MES_QualityInspectionPlanDetail', name: 'MES_QualityInspectionPlanDetail', component: () => import('@/views/mes/mes/TestService.vue') },
  { path: '/MES_QualityInspectionRecord', name: 'MES_QualityInspectionRecord', component: () => import('@/views/mes/mes/MES_QualityInspectionRecord.vue') },
  { path: '/MES_SchedulingPlan', name: 'MES_SchedulingPlan', component: () => import('@/views/mes/mes/MES_SchedulingPlan.vue') },
  { path: '/MES_Bom_Main', name: 'MES_Bom_Main', component: () => import('@/views/mes/mes/MES_Bom_Main.vue') },
  { path: '/MES_Bom_Detail', name: 'MES_Bom_Detail', component: () => import('@/views/mes/mes/MES_Bom_Detail.vue') },
  { path: '/MES_Calendar', name: 'MES_Calendar', component: () => import('@/views/mes/mes/MES_Calendar.vue') },

  // ==================== Vol Demo 路由（保留参考，菜单已移到最后） ====================
  { path: '/flex', name: 'flex', component: () => import('@/views/mes/mes/TestService.vue') },
  { path: '/formChart', name: 'formChart', component: () => import('@/views/mes/mes/TestService.vue') },
  { path: '/list', name: 'mes_list', component: () => import('@/views/mes/mes/TestService.vue') },
  { path: '/pages/order/App_Appointment1/App_Appointment1', name: 'App_Appointment1', component: () => import('@/views/mes/mes/TestService.vue') },
  { path: '/pages/form/form1', name: 'form1', component: () => import('@/views/mes/mes/TestService.vue') },

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
  
  // 标准文件管理（扁平路由，菜单 URL 直接匹配）
  {
    path: '/CertPlatform/Standard/DirectoryConfig',
    name: 'DirectoryConfig',
    component: () => import('@/views/cert/Standard/DirectoryManager/index.vue'),
    meta: { title: '标准文件管理' }
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
  // NC 检查规则（扁平路由）
  {
    path: '/CertPlatform/WorkflowRules/Rules',
    name: 'ValidationRuleList',
    component: () => import('@/views/cert/Standard/WorkflowRules/List.vue'),
    meta: { title: 'NC 检查规则' }
  },
  // 报告章节定义（扁平路由）
  {
    path: '/CertPlatform/WorkflowRules/ReportDef',
    name: 'ReportDefinition',
    component: () => import('@/views/cert/Standard/WorkflowRules/ReportDefinition.vue'),
    meta: { title: '报告章节定义' }
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