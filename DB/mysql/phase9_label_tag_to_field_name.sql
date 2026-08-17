-- ============================================================
-- Phase 9: label_tag → field_name 改名整改（幂等版）
-- 日期: 2026-08-16
-- 方案: C（改名，彻底整改）
-- 关联: 审核规则库与工作流设计器-功能设计-V4-评审报告.md §3.1
-- 说明: 幂等，可重复执行；先检查列是否存在再操作
-- ============================================================

-- ============================================================
-- 0. 创建幂等辅助存储过程
-- ============================================================
DROP PROCEDURE IF EXISTS `p_safe_change_column`;
DROP PROCEDURE IF EXISTS `p_safe_drop_column`;
DROP PROCEDURE IF EXISTS `p_safe_drop_index`;

DELIMITER //
CREATE PROCEDURE `p_safe_change_column`(
  IN p_table VARCHAR(64), IN p_old_col VARCHAR(64), IN p_new_col VARCHAR(64),
  IN p_def VARCHAR(500)
)
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_old_col) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` CHANGE COLUMN `', p_old_col, '` `', p_new_col, '` ', p_def);
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Renamed ', p_table, '.', p_old_col, ' → ', p_new_col) AS result;
  ELSEIF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_new_col) THEN
    SELECT CONCAT('Skipped: ', p_table, ' already has ', p_new_col) AS result;
  ELSE
    SELECT CONCAT('Error: ', p_table, ' has neither ', p_old_col, ' nor ', p_new_col) AS result;
  END IF;
END//

CREATE PROCEDURE `p_safe_drop_column`(IN p_table VARCHAR(64), IN p_col VARCHAR(64))
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_col) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` DROP COLUMN `', p_col, '`');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Dropped ', p_table, '.', p_col) AS result;
  ELSE
    SELECT CONCAT('Skipped: ', p_table, '.', p_col, ' does not exist') AS result;
  END IF;
END//

CREATE PROCEDURE `p_safe_drop_index`(IN p_table VARCHAR(64), IN p_index VARCHAR(64))
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE table_schema = DATABASE() AND table_name = p_table AND index_name = p_index) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` DROP INDEX `', p_index, '`');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Dropped index ', p_index, ' from ', p_table) AS result;
  ELSE
    SELECT CONCAT('Skipped: index ', p_index, ' not found on ', p_table) AS result;
  END IF;
END//
DELIMITER ;

-- ============================================================
-- 1. ent_extraction_result: label_tag → field_name
-- ============================================================
CALL p_safe_change_column('ent_extraction_result', 'label_tag', 'field_name', "varchar(200) DEFAULT NULL COMMENT '字段名称（中文名，展示用，不作为查询键）'");
CALL p_safe_drop_index('ent_extraction_result', 'idx_label_tag');

-- 添加 idx_field_name（如果不存在）
SET @idx_exists = (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE table_schema = DATABASE() AND table_name = 'ent_extraction_result' AND index_name = 'idx_field_name');
SET @sql = IF(@idx_exists = 0, 'ALTER TABLE ent_extraction_result ADD INDEX `idx_field_name` (`field_name`)', 'SELECT "idx_field_name already exists"');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 清洗数据：从 cert_doc_field_def 回填中文名
UPDATE ent_extraction_result r
INNER JOIN cert_doc_field_def f ON r.rule_code = f.rule_code AND r.field_code = f.field_code
SET r.field_name = f.field_name
WHERE r.field_name IS NOT NULL OR r.field_name = '';

-- 兜底：无法关联的用 field_code 填充
UPDATE ent_extraction_result SET field_name = field_code WHERE field_name IS NULL OR field_name = '';

-- ============================================================
-- 2. cert_extraction_field: 删除 label_tag 列（已有 field_name 列）
-- ============================================================

-- 先合并 label_tag 数据到 field_name（仅当 field_name 为空时）
UPDATE cert_extraction_field SET field_name = label_tag WHERE (field_name IS NULL OR field_name = '') AND label_tag IS NOT NULL;

-- 删除旧唯一索引
CALL p_safe_drop_index('cert_extraction_field', 'uk_label_tag');

-- 删除 label_tag 列
CALL p_safe_drop_column('cert_extraction_field', 'label_tag');

-- ============================================================
-- 3. 清理辅助存储过程
-- ============================================================
DROP PROCEDURE IF EXISTS `p_safe_change_column`;
DROP PROCEDURE IF EXISTS `p_safe_drop_column`;
DROP PROCEDURE IF EXISTS `p_safe_drop_index`;

-- ============================================================
-- 4. 验证
-- ============================================================
SELECT '=== ent_extraction_result ===' AS info;
DESCRIBE ent_extraction_result;

SELECT '=== cert_extraction_field ===' AS info;
DESCRIBE cert_extraction_field;

SELECT '=== ent_extraction_result field_name 数据检查 ===' AS info;
SELECT field_code, field_name, COUNT(*) AS cnt
FROM ent_extraction_result
WHERE enterprise_code = 'YZH-STD-ENT'
GROUP BY field_code, field_name
ORDER BY field_code;
