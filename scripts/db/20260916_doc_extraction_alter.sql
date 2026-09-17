-- ==========================================================
-- 文档提取规则模块（业务层 #17）数据库变更
-- 日期：2026-09-16
-- 计划：docs/50-任务/文档提取规则-移植实施计划-V1.md（V1.1）
-- 决策：D-5 提取引擎落共享层 / D-6 预览统一转 PDF、提取统一转 Markdown
--
-- 内容：
--   1. cert_standard_directory_file 补预览/提取产物列
--      （converted_storage_path 语义保留给 doc→docx 中间产物，不动）
--   2. cert_sys_config 补 AI 总开关 ai_extract_enabled
--   3. 校验 cert_doc_extraction_rule 系列表（只查不改，输出核对信息）
--
-- 幂等性：全部语句可重复执行（information_schema 判重 + INSERT ... SELECT WHERE NOT EXISTS）
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform < scripts/db/20260916_doc_extraction_alter.sql
-- ==========================================================

-- ------------------------------------------------------------
-- 1. cert_standard_directory_file 补产物列
--    preview_pdf_path  : LibreOffice 转 PDF 产物（预览专用）
--    markdown_path     : anydoc 转 Markdown 产物（提取专用）
--    markdown_status   : none/pending/converting/completed/failed
--    markdown_message  : 转换失败原因
--    markdown_date     : 完成时间
-- ------------------------------------------------------------

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'preview_pdf_path');
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `preview_pdf_path` varchar(512) DEFAULT NULL COMMENT ''预览PDF产物路径（LibreOffice转换）'' AFTER `convert_date`',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'markdown_path');
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `markdown_path` varchar(512) DEFAULT NULL COMMENT ''提取Markdown产物路径（anydoc转换）'' AFTER `preview_pdf_path`',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'markdown_status');
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `markdown_status` varchar(20) DEFAULT ''none'' COMMENT ''Markdown转换状态：none/pending/converting/completed/failed'' AFTER `markdown_path`',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'markdown_message');
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `markdown_message` varchar(1024) DEFAULT NULL COMMENT ''Markdown转换失败原因'' AFTER `markdown_status`',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file' AND COLUMN_NAME = 'markdown_date');
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE `cert_standard_directory_file` ADD COLUMN `markdown_date` datetime DEFAULT NULL COMMENT ''Markdown转换完成时间'' AFTER `markdown_message`',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 2. cert_sys_config 补 AI 总开关（V1.1 §9.3 决策）
--    ai_extract_enabled = true/false，false 时 analyze/verify 优雅降级
--    说明：api_key 等六键已在库（phase3_queue_tables.sql），此处只补开关
-- ------------------------------------------------------------

INSERT INTO cert_sys_config (ConfigKey, ConfigValue, ConfigType, Category, DisplayName, Description, Sort, IsValid, Status, CreateBy, CreateTime)
SELECT 'ai_extract_enabled', 'true', 'boolean', 'ai_model', 'AI提取总开关',
       '关闭后文档提取规则的AI分析与验证降级为手动模式（字段表格手动定义），规则CRUD不受影响', 0, 1, 'active', 'system', NOW()
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM cert_sys_config WHERE ConfigKey = 'ai_extract_enabled' AND IsDeleted = 0);

-- ------------------------------------------------------------
-- 3. 校验区（只查不改）：核对实体映射基准
-- ------------------------------------------------------------

-- 3.1 规则主表列清单（期望：DocIsValid / CreateBy+CreateTime 体系 / IsDeleted+IsValid）
SELECT 'cert_doc_extraction_rule 期望列核对：DocIsValid(是)/verify_message(是)/sample_data(是)/doc_content(是)/status(是)' AS check_note;
SHOW COLUMNS FROM cert_doc_extraction_rule LIKE 'DocIsValid';

-- 3.2 字段/表格定义表
SHOW COLUMNS FROM cert_doc_field_def LIKE 'is_ai_recommended';
SHOW COLUMNS FROM cert_doc_table_def LIKE 'table_code';
SHOW COLUMNS FROM cert_doc_table_field_def LIKE 'column_code';

-- 3.3 AI 配置与提示词模板现状
SELECT ConfigKey, ConfigValue FROM cert_sys_config WHERE Category = 'ai_model' AND IsDeleted = 0;
SELECT provider, model, is_enabled FROM cert_ai_config WHERE IsDeleted = 0 LIMIT 5;
SELECT prompt_code, version, is_active FROM wf_prompt_template WHERE prompt_code LIKE 'analyze%' ORDER BY prompt_code, version;

-- 3.4 产物列核对
SHOW COLUMNS FROM cert_standard_directory_file LIKE 'markdown%';
SHOW COLUMNS FROM cert_standard_directory_file LIKE 'preview%';
