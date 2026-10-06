-- ============================================================================
-- 20261004_add_essential_summary.sql
-- 扩展企业原始资料画像，增加语义精要字段（针对长文档的 AI 预处理缓存）
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1. 给 cert_enterprise_original_file 增加 EssentialSummary
ALTER TABLE `cert_enterprise_original_file` 
ADD COLUMN `EssentialSummary` text DEFAULT NULL COMMENT '★语义精要（长文档 AI 自动生成的体系认证核心精简内容，用于 Token 缓存）'
AFTER `AnalyzeTime`;

-- 2. 给 cert_enterprise_doc_profile 增加 EssentialSummary
ALTER TABLE `cert_enterprise_doc_profile` 
ADD COLUMN `EssentialSummary` text DEFAULT NULL COMMENT '★语义精要（长文档 AI 提取结果快照）'
AFTER `Summary`;

-- 3. 验证
-- SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, COLUMN_COMMENT 
-- FROM information_schema.COLUMNS 
-- WHERE TABLE_SCHEMA=DATABASE() AND COLUMN_NAME='EssentialSummary';
