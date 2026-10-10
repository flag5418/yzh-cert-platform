-- ====================================================================
-- 技能分类支持两级（大类 → 子类）—— 2026-10-10
--
-- 背景：原分类为扁平一级（MaxLevel=1），ParentCode 实体标记 IsIgnore 不落库。
--       现扩展为两级树结构，需在 Sys_DictionaryList 表加 ParentCode 列。
--
-- 列名风格：Sys_DictionaryList 为 PascalCase（DicName/DicValue/Color/OrderNo），
--          新增 ParentCode 保持同风格。字符集/排序规则与表一致。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 仅当列不存在时添加（幂等）
SET @col_exists = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Sys_DictionaryList'
      AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'ParentCode'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Sys_DictionaryList ADD COLUMN ParentCode varchar(100) COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT ''父节点编码（DicValue）；根级分类为 NULL''',
    'SELECT ''ParentCode column already exists, skipping'' AS msg'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 验证
SELECT CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin AS ColumnName
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'Sys_DictionaryList'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'ParentCode';
