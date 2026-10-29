-- ============================================================================
-- 20261029_doc_extract_prompt_migrate_V1.sql
-- 将 cert_doc_extract_rules.PromptContent 移植到 cert_prompt_template
--
-- 背景：
--   20261029 建 cert_prompt_template 统一表时，doc_extract 只种了一条
--   247 字符的平台极简占位符，4 份完整的标准提示词遗留在了旧表
--   cert_doc_extract_rules。前端已切到新端点（读新表），导致页面展示
--   "你是一名文档信息提取专家..." 而非完整的逐条款提取提示词。
--
-- 修复：
--   将旧表 PromptContent 迁入新表，ScopeKey = cert_doc_extract_rules.StandardCode（GUID）。
--   幂等：ON DUPLICATE KEY UPDATE 只更新 Template + 审计字段。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- 4 份标准提示词 → cert_prompt_template（PromptCode = 'doc_extract'）
-- ScopeKey 对齐前端传入的 rule.StandardCode（GUID 格式）
-- ────────────────────────────────────────────────────────────────────────────

INSERT INTO `cert_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Remark`, `Sort`, `IsValid`, `Status`, `IsDeleted`, `CreateBy`, `CreateTime`)
SELECT
  REPLACE(UUID(), '-', ''),
  'doc_extract',
  CONCAT('文档提取 - ', r.StandardName),
  NULL,
  r.StandardCode,
  r.PromptContent,
  CONCAT('从旧表 cert_doc_extract_rules 迁移（Id=', r.Id, '）。原 CreateBy=', COALESCE(r.CreateBy, 'unknown')),
  100,
  1,
  'active',
  0,
  'migration_20261029',
  NOW()
FROM `cert_doc_extract_rules` r
WHERE r.PromptContent IS NOT NULL
  AND LENGTH(r.PromptContent) > 100
ON DUPLICATE KEY UPDATE
  `Template` = VALUES(`Template`),
  `PromptName` = VALUES(`PromptName`),
  `Remark` = VALUES(`Remark`),
  `UpdateBy` = 'migration_20261029',
  `UpdateTime` = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- 验证
-- ────────────────────────────────────────────────────────────────────────────
SELECT
  PromptCode,
  ScopeKey,
  OrgCode,
  IsValid,
  LENGTH(Template) AS len,
  CreateBy,
  LEFT(Template, 60) AS snippet
FROM `cert_prompt_template`
WHERE PromptCode = 'doc_extract'
ORDER BY Sort;
