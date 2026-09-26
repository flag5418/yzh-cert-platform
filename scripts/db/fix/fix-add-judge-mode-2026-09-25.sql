-- =====================================================================
-- fix-add-judge-mode-2026-09-25.sql
--
-- 目的：NC 检查项（cert_validation_rule）新增「判定方式」字段 JudgeMode
--
-- 背景（用户 2026-09-25 需求）：
--   一些检查项是**人为调研**发现的，AI 不可能知道 —— 例如「某个该有的
--   设备是否存在」。因此检查项需要区分「AI 自动判定 / 人工判定 / 半自动」。
--   术语是**「判定方式」**，不是「复核」。
--
-- 值域：auto = AI 自动判定 / manual = 人工判定 / semi = 半自动（AI 出初判 + 人工确认）
--
-- 遵守铁律：
--   铁律七  列名 PascalCase，且 DB 列名 = C# 属性名 = TS 字段名
--   铁律八  全库统一 utf8mb4_general_ci，DDL 显式带 COLLATE
--   铁律九  禁 Enable 列；启禁用统一走 IsValid（本表已有 IsValid）
--   准则 A  Id 不作业务键；本列是业务属性，不参与定位
--
-- 幂等：先查 information_schema，列已存在则跳过
--   ⚠️ 必须用 BINARY 精确比对 —— information_schema.COLUMN_NAME 的排序
--      规则大小写不敏感，直接 = 'JudgeMode' 会同时命中 judgemode（假阴性陷阱）
-- =====================================================================

SET @db := DATABASE();

SET @exists := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db
    AND TABLE_NAME = 'cert_validation_rule'
    AND BINARY COLUMN_NAME = 'JudgeMode'
);

SET @ddl := IF(@exists = 0,
  'ALTER TABLE `cert_validation_rule`
     ADD COLUMN `JudgeMode` varchar(20)
       CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
       NOT NULL DEFAULT ''auto''
       COMMENT ''判定方式：auto=AI自动判定 / manual=人工判定 / semi=半自动''
     AFTER `SeverityIfViolated`',
  'SELECT ''JudgeMode 列已存在，跳过'' AS msg');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- =====================================================================
-- 验证：应返回 1 行，列名精确为 JudgeMode（区分大小写）
-- =====================================================================
SELECT
  TABLE_NAME,
  COLUMN_NAME,
  COLUMN_TYPE,
  IS_NULLABLE,
  COLUMN_DEFAULT,
  COLLATION_NAME,
  COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db
  AND TABLE_NAME = 'cert_validation_rule'
  AND BINARY COLUMN_NAME = 'JudgeMode';

-- 全表列名大小写体检（应 0 行）：任何列名不符合 PascalCase 即为违规
SELECT CONCAT('命名违规列: ', TABLE_NAME, '.', COLUMN_NAME) AS violation
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db
  AND TABLE_NAME = 'cert_validation_rule'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z][A-Za-z0-9]*$';

-- 字符集体检（应 0 行）：任何非 utf8mb4_general_ci 的字符列即为违规
SELECT CONCAT('字符集违规列: ', TABLE_NAME, '.', COLUMN_NAME, ' = ', COLLATION_NAME) AS violation
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db
  AND TABLE_NAME = 'cert_validation_rule'
  AND COLLATION_NAME IS NOT NULL
  AND COLLATION_NAME <> 'utf8mb4_general_ci';
