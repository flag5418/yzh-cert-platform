-- =============================================================================
-- 20261111_enterprise_doc_profile_V1.sql
-- 目的：给 cert_enterprise_doc 增加「文档简介 + 目录索引」字段。
--   DocProfileJson        = 文档简介 / 目录（JSON，绑定 Sha256 + VersionNumber）
--   DocProfileUpdatedTime = 生成时间
-- 幂等：information_schema 判断列是否存在，存在则跳过。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
SET @db := DATABASE();

SET @sql := (
  SELECT IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_doc'
        AND COLUMN_NAME = 'DocProfileJson') = 0,
    'ALTER TABLE cert_enterprise_doc ADD COLUMN DocProfileJson LONGTEXT NULL COMMENT ''文档简介+目录索引 JSON（绑定 Sha256/VersionNumber）''',
    'SELECT ''DocProfileJson already exists'' AS skip_msg'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (
  SELECT IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_doc'
        AND COLUMN_NAME = 'DocProfileUpdatedTime') = 0,
    'ALTER TABLE cert_enterprise_doc ADD COLUMN DocProfileUpdatedTime DATETIME NULL COMMENT ''文档简介生成时间''',
    'SELECT ''DocProfileUpdatedTime already exists'' AS skip_msg'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db
  AND TABLE_NAME = 'cert_enterprise_doc'
  AND COLUMN_NAME IN ('DocProfileJson', 'DocProfileUpdatedTime');
