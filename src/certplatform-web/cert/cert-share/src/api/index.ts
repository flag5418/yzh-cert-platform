// Share API 入口 - admin + auditor 共用业务 API
// System 域 API：user / role / role-menu 等仍为 admin 独有（在 admin/src/api/system/ 下）；
//   menu 已上移到本层（cert-share/src/api/system/menu.ts），admin 与 auditor 共用。
//   使用方请直接 `from '@share/api/system/menu'` 导入 —— 未从本 barrel 再导出，避免命名冲突。

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
export * from './workflow/nc-config'
export * from './workflow/report-rule'
export * from './workflow/doc-extraction-rule'
export * from './workflow/execution'

// System 域 API
export * from './system-log'
