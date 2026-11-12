-- =============================================================================
-- 20261111_enterprise_doc_section_map_V1.sql
-- 目的：给 cert_enterprise_doc 增加「手册章节索引」字段（L1）。
--   SectionMapJson        = 标准条款 → 手册小节映射（含 CharStart/CharEnd/folded）
--   SectionMapUpdatedTime = 生成时间
-- 幂等：information_schema 判断列是否存在，存在则跳过。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
SET @db := DATABASE();

SET @sql := (
  SELECT IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_doc'
        AND COLUMN_NAME = 'SectionMapJson') = 0,
    'ALTER TABLE cert_enterprise_doc ADD COLUMN SectionMapJson LONGTEXT NULL COMMENT ''手册章节索引 JSON（标准条款→小节，含 folded）''',
    'SELECT ''SectionMapJson already exists'' AS skip_msg'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (
  SELECT IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_enterprise_doc'
        AND COLUMN_NAME = 'SectionMapUpdatedTime') = 0,
    'ALTER TABLE cert_enterprise_doc ADD COLUMN SectionMapUpdatedTime DATETIME NULL COMMENT ''章节索引生成时间''',
    'SELECT ''SectionMapUpdatedTime already exists'' AS skip_msg'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db
  AND TABLE_NAME = 'cert_enterprise_doc'
  AND COLUMN_NAME IN ('SectionMapJson', 'SectionMapUpdatedTime');
