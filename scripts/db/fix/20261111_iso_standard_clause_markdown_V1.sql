-- 2026-11-11 V3 知识层：标准「条款 Markdown」落在标准行上
-- 目的：上传标准文件 → 原始 Markdown（MarkdownPath，事实源）
--       → 按原文生成条款 → 条款 Markdown（ClauseMarkdownPath，派生视图，供提示词/预览）
-- 幂等：列已存在则跳过（MySQL 8.0 无 ADD COLUMN IF NOT EXISTS）
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SET @db = 'yzh_cert_platform';
SET @tbl = 'cert_iso_standard';

-- ClauseMarkdownPath
SET @sql = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tbl AND COLUMN_NAME = 'ClauseMarkdownPath') = 0,
  'ALTER TABLE cert_iso_standard ADD COLUMN ClauseMarkdownPath VARCHAR(500) NULL COMMENT ''条款 Markdown 存储路径（派生，/standard-source/{Code}/clauses/clauses.md）'' AFTER MarkdownUpdatedTime',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ClauseMarkdownChars
SET @sql = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tbl AND COLUMN_NAME = 'ClauseMarkdownChars') = 0,
  'ALTER TABLE cert_iso_standard ADD COLUMN ClauseMarkdownChars INT NOT NULL DEFAULT 0 COMMENT ''条款 Markdown 字符数（0=未生成）'' AFTER ClauseMarkdownPath',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ClauseMarkdownUpdatedTime
SET @sql = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tbl AND COLUMN_NAME = 'ClauseMarkdownUpdatedTime') = 0,
  'ALTER TABLE cert_iso_standard ADD COLUMN ClauseMarkdownUpdatedTime DATETIME NULL COMMENT ''条款 Markdown 最近生成时间（本地时间）'' AFTER ClauseMarkdownChars',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT Code, StandardCode, StandardName, MarkdownPath, MarkdownChars,
       ClauseMarkdownPath, ClauseMarkdownChars, ClauseMarkdownUpdatedTime
FROM cert_iso_standard
WHERE IsDeleted = 0
ORDER BY Sort;
