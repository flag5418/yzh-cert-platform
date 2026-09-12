// Share API 入口 - admin + auditor 共用业务 API
// System 域 API（user/role/menu/...）admin 独有，已移至 admin/src/api/system/

// 通用 CRUD 客户端（admin + auditor 共用）
export * from './generic'

// 认证域 API（admin + auditor 共用）
export * from './auth'

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
export * from './workflow/queue'
export * from './workflow/nc-config'
export * from './workflow/report-rule'
export * from './workflow/doc-extraction'
