-- =============================================================================
-- 20261111_enterprise_original_file_nullable_times_V1.sql
-- 目的：cert_enterprise_original_file 的 ConvertDate / AnalyzeTime / PolicyDecidedTime
--       允许 NULL。业务层上传后先落「未转换/未分析」行，这三列本来就是空的；
--       MySQL 严格模式下 NOT NULL 会直接拒绝新增，页面表现为「必填字段为空」。
-- 幂等：先查 information_schema.IS_NULLABLE，已是 YES 则跳过。
-- 环境：MySQL 8.0.46（无 ADD COLUMN IF NOT EXISTS；这里用 PREPARE 动态执行）
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SET @db := DATABASE();

-- ConvertDate
SET @sql := (
  SELECT IF(
    (SELECT IS_NULLABLE FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_original_file'
        AND COLUMN_NAME = 'ConvertDate') = 'NO',
    'ALTER TABLE cert_enterprise_original_file MODIFY COLUMN ConvertDate DATETIME NULL',
    'SELECT ''ConvertDate already nullable'' AS skip_msg'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- AnalyzeTime
SET @sql := (
  SELECT IF(
    (SELECT IS_NULLABLE FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_original_file'
        AND COLUMN_NAME = 'AnalyzeTime') = 'NO',
    'ALTER TABLE cert_enterprise_original_file MODIFY COLUMN AnalyzeTime DATETIME NULL',
    'SELECT ''AnalyzeTime already nullable'' AS skip_msg'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- PolicyDecidedTime
SET @sql := (
  SELECT IF(
    (SELECT IS_NULLABLE FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_original_file'
        AND COLUMN_NAME = 'PolicyDecidedTime') = 'NO',
    'ALTER TABLE cert_enterprise_original_file MODIFY COLUMN PolicyDecidedTime DATETIME NULL',
    'SELECT ''PolicyDecidedTime already nullable'' AS skip_msg'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 验证
SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db
  AND TABLE_NAME = 'cert_enterprise_original_file'
  AND COLUMN_NAME IN ('ConvertDate', 'AnalyzeTime', 'PolicyDecidedTime');
