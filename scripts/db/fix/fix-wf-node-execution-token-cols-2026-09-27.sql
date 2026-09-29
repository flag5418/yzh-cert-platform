-- ============================================================
-- 修复：wf_node_execution 缺少 LLM Token 统计三列
-- 症状：/business/nc-config 工作流执行失败
--       "Unknown column 'PromptTokens' in 'field list'"
-- 原因：实体 WfNodeExecution 已声明 PromptTokens/CompletionTokens/LlmDurationMs
--       （见 CertPlatform.Shared/Entities/Wf/WfNodeExecution.cs:74-80），
--       但 DB 表未同步增列（实体 vs DB 列差异）
-- 日期：2026-09-27
-- 幂等：按 information_schema 判存后再 ADD，可重复执行
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1. PromptTokens
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'PromptTokens');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `PromptTokens` int NULL DEFAULT NULL COMMENT ''LLM Prompt Tokens（仅 ai_node 有值）'' AFTER `ExecutionTimeMs`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 2. CompletionTokens
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'CompletionTokens');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `CompletionTokens` int NULL DEFAULT NULL COMMENT ''LLM Completion Tokens（仅 ai_node 有值）'' AFTER `PromptTokens`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 3. LlmDurationMs
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'LlmDurationMs');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `LlmDurationMs` int NULL DEFAULT NULL COMMENT ''纯 LLM API 调用耗时(ms)'' AFTER `CompletionTokens`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ============================================================
-- 验证（应返回 3 行）
-- ============================================================
-- SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_COMMENT
--   FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution'
--    AND COLUMN_NAME IN ('PromptTokens','CompletionTokens','LlmDurationMs');
