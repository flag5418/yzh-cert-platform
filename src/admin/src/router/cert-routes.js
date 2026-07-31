/**
 * 认证平台路由配置
 * 路径：/cert/*
 * 包含：机构管理、标准管理、申请管理、审核任务等
 */

export default [
  {
    path: '/cert',
    name: 'CertPlatform',
    component: () => import('@/views/layout/Layout.vue'),
    meta: { title: '认证平台', icon: 'el-icon-s-cooperation' },
    children: [
      // ==================== 基础配置模块 ====================
      {
        path: '/cert/certification-body',
        name: 'CertificationBody',
        component: () => import('@/views/cert/CertificationBody/CertificationBody.vue'),
        meta: { 
          title: '认证机构管理', 
          icon: 'el-icon-office-building',
          requireAuth: true,
        },
      },
      {
        path: '/cert/iso-standard',
        name: 'ISOStandard',
        component: () => import('@/views/cert/ISOStandard/ISOStandard.vue'),
        meta: { 
          title: 'ISO 标准管理', 
          icon: 'el-icon-document',
          requireAuth: true,
        },
      },
      {
        path: '/cert/iso-clause',
        name: 'ISOClause',
        component: () => import('@/views/cert/ISOClause/ISOClause.vue'),
        meta: { 
          title: '标准条款管理', 
          icon: 'el-icon-tickets',
          requireAuth: true,
        },
      },
      
      // ==================== 企业与申请模块 ====================
      {
        path: '/cert/enterprise',
        name: 'Enterprise',
        component: () => import('@/views/cert/Enterprise/Enterprise.vue'),
        meta: { 
          title: '企业管理', 
          icon: 'el-icon-school',
          requireAuth: true,
        },
      },
      {
        path: '/cert/cert-application',
        name: 'CertApplication',
        component: () => import('@/views/cert/CertApplication/CertApplication.vue'),
        meta: { 
          title: '认证申请管理', 
          icon: 'el-icon-edit-outline',
          requireAuth: true,
        },
      },

      // ==================== 审核执行模块 ====================
      {
        path: '/cert/audit-project',
        name: 'AuditProject',
        component: () => import('@/views/cert/AuditProject/AuditProject.vue'),
        meta: { 
          title: '审核项目管理', 
          icon: 'el-icon-date',
          requireAuth: true,
        },
      },
      {
        path: '/cert/audit-task',
        name: 'AuditTask',
        component: () => import('@/views/cert/AuditTask/AuditTask.vue'),
        meta: { 
          title: '审核任务管理', 
          icon: 'el-icon-finished',
          requireAuth: true,
        },
      },
      {
        path: '/cert/checklist-item',
        name: 'ChecklistItem',
        component: () => import('@/views/cert/ChecklistItem/ChecklistItem.vue'),
        meta: { 
          title: '检查表管理', 
          icon: 'el-icon-list-document',
          requireAuth: true,
        },
      },
      {
        path: '/cert/nonconformity',
        name: 'NonConformity',
        component: () => import('@/views/cert/NonConformity/NonConformity.vue'),
        meta: { 
          title: '不符合项管理', 
          icon: 'el-icon-warning',
          requireAuth: true,
        },
      },

      // ==================== 报告与证书模块 ====================
      {
        path: '/cert/audit-report',
        name: 'AuditReport',
        component: () => import('@/views/cert/AuditReport/AuditReport.vue'),
        meta: { 
          title: '审核报告管理', 
          icon: 'el-icon-notebook-2',
          requireAuth: true,
        },
      },
      {
        path: '/cert/certificate',
        name: 'Certificate',
        component: () => import('@/views/cert/Certificate/Certificate.vue'),
        meta: { 
          title: '证书管理', 
          icon: 'el-icon-medal',
          requireAuth: true,
        },
      },
    ],
  },
];

/**
 * 菜单配置（用于动态菜单生成）
 */
export const certMenuConfig = [
  {
    id: 'cert_platform',
    name: '认证平台',
    icon: 'el-icon-s-cooperation',
    orderNo: 100,
    children: [
      // 基础配置
      {
        id: 'cert_body',
        name: '认证机构管理',
        path: '/cert/certification-body',
        icon: 'el-icon-office-building',
        orderNo: 101,
      },
      {
        id: 'cert_standard',
        name: 'ISO 标准管理',
        path: '/cert/iso-standard',
        icon: 'el-icon-document',
        orderNo: 102,
      },
      {
        id: 'cert_clause',
        name: '标准条款管理',
        path: '/cert/iso-clause',
        icon: 'el-icon-tickets',
        orderNo: 103,
      },
      
      // 业务流程
      {
        id: 'cert_enterprise',
        name: '企业管理',
        path: '/cert/enterprise',
        icon: 'el-icon-school',
        orderNo: 110,
      },
      {
        id: 'cert_application',
        name: '认证申请管理',
        path: '/cert/cert-application',
        icon: 'el-icon-edit-outline',
        orderNo: 111,
      },
      
      // 审核执行
      {
        id: 'cert_audit_project',
        name: '审核项目管理',
        path: '/cert/audit-project',
        icon: 'el-icon-date',
        orderNo: 120,
      },
      {
        id: 'cert_audit_task',
        name: '审核任务管理',
        path: '/cert/audit-task',
        icon: 'el-icon-finished',
        orderNo: 121,
      },
      {
        id: 'cert_checklist',
        name: '检查表管理',
        path: '/cert/checklist-item',
        icon: 'el-icon-list-document',
        orderNo: 122,
      },
      {
        id: 'cert_nc',
        name: '不符合项管理',
        path: '/cert/nonconformity',
        icon: 'el-icon-warning',
        orderNo: 123,
      },
      
      // 报告证书
      {
        id: 'cert_report',
        name: '审核报告管理',
        path: '/cert/audit-report',
        icon: 'el-icon-notebook-2',
        orderNo: 130,
      },
      {
        id: 'cert_certificate',
        name: '证书管理',
        path: '/cert/certificate',
        icon: 'el-icon-medal',
        orderNo: 131,
      },
    ],
  },
];
