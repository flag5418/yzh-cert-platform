-- ============================================================
-- 命名规范校验（★ 只读，不做任何修改）
-- 铁律七：DB列名 = C#属性名 = TS字段名 = PascalCase 逐字一致
-- 铁律九：全库禁止 `Enable` 列；启禁语义统一走 IIsValid.IsValid（1=有效 0=无效）
--
-- 用法：
--   docker exec -i yzh-mysql mysql -uroot -p*** --default-character-set=utf8mb4 \
--     yzh_cert_platform < scripts/db/verify/verify_naming.sql
--
-- 期望：第 1、2 行违规数为 0
--
-- ⚠️⚠️ 为什么必须写 CONVERT(... COLLATE utf8mb4_bin)：
--   information_schema.COLUMN_NAME 的排序规则是 _ci（大小写不敏感），
--   直接写 `COLUMN_NAME NOT REGEXP '^[A-Z][A-Za-z0-9]*$'` 时，
--   `id` 的 `i` 会被当作 [A-Z] 匹配 → 该查询 **恒返回 0 行**（假阴性）。
--   即：错误的校验 SQL 会一直告诉你「已修好」，而库里其实还有上百列违规。
--   另：BINARY COLUMN_NAME 不能配 REGEXP（ERROR 3995），但可以配 IN (...)。
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- 汇总（期望前两行为 0） ----------
SELECT '1_非PascalCase列' AS CheckItem, COUNT(*) AS Violations
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND LEFT(TABLE_NAME, 5) <> '_bak_'
   AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin
       NOT REGEXP '^[A-Z][A-Za-z0-9]*$'
UNION ALL
SELECT '2_Enable列', COUNT(*)
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND LEFT(TABLE_NAME, 5) <> '_bak_'
   AND BINARY COLUMN_NAME IN ('enable', 'Enable')
UNION ALL
SELECT '3_正式表数', COUNT(*)
  FROM information_schema.TABLES
 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
   AND LEFT(TABLE_NAME, 5) <> '_bak_'
UNION ALL
SELECT '4_视图数', COUNT(*)
  FROM information_schema.TABLES
 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'VIEW';

-- ---------- 明细（违规为 0 时本段返回空集） ----------
SELECT c.TABLE_NAME AS TableName,
       c.COLUMN_NAME AS ColumnName,
       CASE WHEN BINARY c.COLUMN_NAME IN ('enable', 'Enable')
            THEN 'Enable列（铁律九）'
            ELSE '非PascalCase（铁律七）' END AS Violation
  FROM information_schema.COLUMNS c
 WHERE c.TABLE_SCHEMA = DATABASE()
   AND LEFT(c.TABLE_NAME, 5) <> '_bak_'
   AND ( CONVERT(c.COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin
         NOT REGEXP '^[A-Z][A-Za-z0-9]*$'
      OR BINARY c.COLUMN_NAME IN ('enable', 'Enable') )
 ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;

-- ---------- 启禁字段覆盖（铁律九：应有 IsValid） ----------
SELECT '缺IsValid的正式表数' AS CheckItem, COUNT(*) AS Tables
  FROM information_schema.TABLES t
 WHERE t.TABLE_SCHEMA = DATABASE() AND t.TABLE_TYPE = 'BASE TABLE'
   AND LEFT(t.TABLE_NAME, 5) <> '_bak_'
   AND t.TABLE_NAME NOT LIKE 'sys\_%'
   AND t.TABLE_NAME NOT LIKE 'Sys\_%'
   AND NOT EXISTS (
       SELECT 1 FROM information_schema.COLUMNS c
        WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA
          AND c.TABLE_NAME = t.TABLE_NAME
          AND BINARY c.COLUMN_NAME = 'IsValid');
