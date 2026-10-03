-- ============================================================
-- cert_doc_table_def 补 IsManual 列（2026-09-30）
--
-- 背景：任务开启时要按「规则里标记为必填的字段/表格」生成补录清单。
--       字段侧 cert_doc_field_def.IsManual 早已存在（"是否需手动补充：0-否 1-是"），
--       但表格侧 cert_doc_table_def **缺**该标志 ⇒ 表格无法参与补录清单判定。
--
-- 影响：不加此列，「表格的补录清单」永远为空。
-- 幂等：先查 information_schema，已存在则跳过。
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -p$MYSQL_ROOT_PASSWORD \
--         --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- ============================================================

SET @exist := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_doc_table_def'
    AND COLUMN_NAME  = 'IsManual');

SET @ddl := IF(@exist = 0,
  'ALTER TABLE cert_doc_table_def ADD COLUMN IsManual tinyint(1) NOT NULL DEFAULT 0 COMMENT ''是否需手动补充：0-否 1-是'' AFTER Description',
  'SELECT ''IsManual 已存在，跳过'' AS msg');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ── 验证 ──
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME   = 'cert_doc_table_def'
  AND COLUMN_NAME  = 'IsManual';
