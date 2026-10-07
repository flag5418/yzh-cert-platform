-- ============================================================================
-- M6 · 企业资料画像「按标准」维度修复（第 1.5 批）
-- 2026-10-06
--
-- 背景（`60` §二 M6）：
--   一个企业 × 阶段可以关联**多个标准**（`cert_enterprise_stage`），而语义分析的
--   `doc_group` / `doc_content` 提示词是**按标准绑定**的 ⇒ 同一份原始资料在标准 A 与
--   标准 B 下的「分组 / 文档作用」**本就不同**，必须按标准各分析一次、各产一行画像
--   （已裁：选项 A 多行画像，`60` §六之补四）。
--
-- 现状（实测 2026-10-06）：
--   `uk_file_ver` = (OriginalFileCode, ProfileVersion)  ⇒ **不含 StandardCode**
--   ⇒ 多标准写入会**撞唯一键**；且执行器的 latest 查询不含标准 ⇒ 后分析的标准顶掉先前的画像。
--
-- 本迁移只做一件事：把唯一键扩为 (OriginalFileCode, StandardCode, ProfileVersion)。
--   · 该表设计**本就预留了标准维度**（`idx_ent_std_stage` = Enterprise+Standard+Stage）
--   · `StandardCode` 列已是 `varchar(36) NOT NULL DEFAULT ''`（⛔ 非 NULL ⇒ 不会绕过唯一性）
--   · 迁移前实测 101 行、StandardCode 全为 `846dec4b-…` ⇒ **无重复元组，可直接加索引**
--
-- ⛔ 幂等：重复执行不会报错（先判断索引是否存在）。
-- ============================================================================

SET NAMES utf8mb4;

-- ── 1. 迁移前自检：确认没有会撞新唯一键的重复元组 ────────────────────────────
-- 期望 0 行。若有行 ⇒ **先停下来**，人工决定保留哪一行（本脚本不做数据清理）。
SELECT '迁移前重复元组自检（期望 0 行）' AS Step;
SELECT OriginalFileCode, StandardCode, ProfileVersion, COUNT(*) AS Cnt
FROM cert_enterprise_doc_profile
GROUP BY OriginalFileCode, StandardCode, ProfileVersion
HAVING COUNT(*) > 1;

-- ── 2. 加新唯一键（若尚未存在） ─────────────────────────────────────────────
SET @has_new := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_enterprise_doc_profile'
    AND INDEX_NAME   = 'uk_file_std_ver'
);

SET @sql_add := IF(@has_new = 0,
  'ALTER TABLE cert_enterprise_doc_profile
     ADD UNIQUE KEY `uk_file_std_ver` (`OriginalFileCode`,`StandardCode`,`ProfileVersion`)',
  'SELECT ''uk_file_std_ver 已存在，跳过'' AS Note');
PREPARE s1 FROM @sql_add; EXECUTE s1; DEALLOCATE PREPARE s1;

-- ── 3. 删旧唯一键（若仍存在） ───────────────────────────────────────────────
SET @has_old := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'cert_enterprise_doc_profile'
    AND INDEX_NAME   = 'uk_file_ver'
);

SET @sql_drop := IF(@has_old > 0,
  'ALTER TABLE cert_enterprise_doc_profile DROP INDEX `uk_file_ver`',
  'SELECT ''uk_file_ver 已不存在，跳过'' AS Note');
PREPARE s2 FROM @sql_drop; EXECUTE s2; DEALLOCATE PREPARE s2;

-- ── 4. 验证：索引应只剩 uk_file_std_ver（不含旧 uk_file_ver） ────────────────
SELECT '迁移后索引（期望见 uk_file_std_ver）' AS Step;
SHOW INDEX FROM cert_enterprise_doc_profile;

-- ── 5. 验证：列名大小写（⛔ 必须带 COLLATE utf8mb4_bin，否则永远 0 行 = 假阴性） ──
SELECT '小写列名自检（期望 0 行）' AS Step;
SELECT COUNT(*) AS LowercaseCols
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME   = 'cert_enterprise_doc_profile'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]';
