// Share API 入口 - admin + auditor 共用业务 API
// System 域：user / role / role-menu 等为 admin 独有（在 admin/src/api/system/ 下）；
//   menu / 认证域 auth 已上移 **yzh.vue.core**（系统底座五原子之原子 API），
//   使用方请 `from '@yzh-core/api/system/menu'` / `'@yzh-core/api/auth'` 导入。

// 通用 CRUD 客户端（admin + auditor 共用）
export * from './generic'

// Foundation 域 API（ISO 标准体系主数据，admin + auditor 共用）
export * from './cert/iso-standard'
export * from './cert/iso-clause'
export * from './cert/phase-definition'
export * from './cert/certification-body'

// Workflow 域 API（审核流程与业务管理，admin + auditor 共用）
export * from './workflow/enterprise'
export * from './workflow/prompt-template'
export * from './workflow/job-skill'
export * from './workflow/ai-usage'
export * from './workflow/nc-config'
export * from './workflow/report-rule'
export * from './workflow/doc-extraction-rule'
export * from './workflow/execution'

// System 域 API
export * from './system-log'
