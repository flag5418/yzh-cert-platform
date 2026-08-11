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
  
  // 标准目录管理（基础配置子菜单）- 使用嵌套路由
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
        meta: { title: '目录配置' }
      },
      {
        path: 'DirectoryTree',
        name: 'DirectoryTree',
        component: () => import('@/views/cert/Standard/DirectoryTree/index.vue'),
        meta: { title: '文件夹结构' }
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
  // 转换队列监控
  {
    path: '/CertPlatform/ConvertQueueMonitor',
    name: 'ConvertQueueMonitor',
    component: () => import('@/views/cert/Standard/ConvertQueueMonitor/index.vue'),
    meta: { title: '转换队列监控' }
  },
  // Prompt 模板管理
  {
    path: '/CertPlatform/PromptTemplate',
    name: 'PromptTemplate',
    component: () => import('@/views/cert/Standard/PromptTemplate/index.vue'),
    meta: { title: 'Prompt 模板管理' }
  }
]

//上面的demo、MES开头的都是示例菜单，可以任意删除 
export default viewgird