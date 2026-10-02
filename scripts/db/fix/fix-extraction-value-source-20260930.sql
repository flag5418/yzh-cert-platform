-- ============================================================
-- fix-extraction-value-source-20260930.sql
--
-- 背景（25 号 §五 · 用户裁决 J1/J2/J4）：
--   J2 补录直接写 cert_extraction_result / cert_table_extraction_result，
--      用 ValueSource 区分 auto（系统自动提取）/ manual（人工录入），缺失记录 INSERT。
--   J4 1 文件 = 1 RuleCode ⇒ 取数与清理的主键是 RuleCode，不是 FileCode。
--      ⇒ 需要 (OrgCode, RuleCode, FieldCode/TableCode) 收窄索引（B2：现状按企业+字段粗取，
--        候选行过多；实测同企业 projectName 有 4 行 IsValid=1 跨 4 个文件）。
--   J1 cert_expert_task_data_gap 记录粒度降为 FieldCode ⇒ GapKey 重定义。
--
-- ⚠️ 06 号 §4.3 早已写过同类 DDL（fix-extraction-manual-columns-2026-09-29.sql），
--    ★ 但至今未执行 —— DB 实测三列均不存在。本脚本以 information_schema 判存，保持幂等。
--
-- ★ 零回填：DEFAULT 'auto' 让存量 95 行自动落 auto，不写全表 UPDATE。
--
-- 幂等：可重复执行。
-- ============================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────
-- 1. ValueSource（字段级）
-- ────────────────────────────────────────────────────────────
SET @c1 := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_extraction_result'
    AND COLUMN_NAME = 'ValueSource');
SET @s1 := IF(@c1 = 0,
  'ALTER TABLE cert_extraction_result
     ADD COLUMN `ValueSource` varchar(20) NOT NULL DEFAULT ''auto''
       COMMENT ''值来源：auto=系统自动提取 | manual=人工录入（25 号 J2）'' AFTER `IsManualEdited`',
  'SELECT ''cert_extraction_result.ValueSource 已存在，跳过'' AS msg');
PREPARE x1 FROM @s1; EXECUTE x1; DEALLOCATE PREPARE x1;

-- ────────────────────────────────────────────────────────────
-- 2. IsManualEdited + ValueSource（表格级 · ★ 原本两列都没有 ⇒ 不对称）
-- ────────────────────────────────────────────────────────────
SET @c2 := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_table_extraction_result'
    AND COLUMN_NAME = 'IsManualEdited');
SET @s2 := IF(@c2 = 0,
  'ALTER TABLE cert_table_extraction_result
     ADD COLUMN `IsManualEdited` tinyint(1) NOT NULL DEFAULT 0
       COMMENT ''是否被人工修改（与 cert_extraction_result 对齐）'' AFTER `PositionInfo`',
  'SELECT ''cert_table_extraction_result.IsManualEdited 已存在，跳过'' AS msg');
PREPARE x2 FROM @s2; EXECUTE x2; DEALLOCATE PREPARE x2;

SET @c3 := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_table_extraction_result'
    AND COLUMN_NAME = 'ValueSource');
SET @s3 := IF(@c3 = 0,
  'ALTER TABLE cert_table_extraction_result
     ADD COLUMN `ValueSource` varchar(20) NOT NULL DEFAULT ''auto''
       COMMENT ''值来源：auto=系统自动提取 | manual=人工录入（25 号 J2）'' AFTER `IsManualEdited`',
  'SELECT ''cert_table_extraction_result.ValueSource 已存在，跳过'' AS msg');
PREPARE x3 FROM @s3; EXECUTE x3; DEALLOCATE PREPARE x3;

-- ────────────────────────────────────────────────────────────
-- 3. RuleCode 收窄索引（B2 / J4）
-- ────────────────────────────────────────────────────────────
SET @i1 := (SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_extraction_result'
    AND INDEX_NAME = 'idx_ent_rule_field');
SET @t1 := IF(@i1 = 0,
  'ALTER TABLE cert_extraction_result
     ADD KEY `idx_ent_rule_field` (`OrgCode`, `RuleCode`, `FieldCode`, `IsValid`)',
  'SELECT ''idx_ent_rule_field 已存在，跳过'' AS msg');
PREPARE y1 FROM @t1; EXECUTE y1; DEALLOCATE PREPARE y1;

SET @i2 := (SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_table_extraction_result'
    AND INDEX_NAME = 'idx_ent_rule_table');
SET @t2 := IF(@i2 = 0,
  'ALTER TABLE cert_table_extraction_result
     ADD KEY `idx_ent_rule_table` (`OrgCode`, `RuleCode`, `TableCode`, `IsValid`)',
  'SELECT ''idx_ent_rule_table 已存在，跳过'' AS msg');
PREPARE y2 FROM @t2; EXECUTE y2; DEALLOCATE PREPARE y2;

-- ────────────────────────────────────────────────────────────
-- 4. ★ J1/J4：cert_expert_task_data_gap 补 RuleCode 列
-- ────────────────────────────────────────────────────────────
SET @c4 := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_expert_task_data_gap'
    AND COLUMN_NAME = 'RuleCode');
SET @s4 := IF(@c4 = 0,
  'ALTER TABLE cert_expert_task_data_gap
     ADD COLUMN `RuleCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL
       COMMENT ''★规则编码（cert_doc_extraction_rule.Code；J4：1文件=1规则，取数与清理的主键成分）'' AFTER `TaskCode`',
  'SELECT ''cert_expert_task_data_gap.RuleCode 已存在，跳过'' AS msg');
PREPARE x4 FROM @s4; EXECUTE x4; DEALLOCATE PREPARE x4;

SET @i3 := (SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_expert_task_data_gap'
    AND INDEX_NAME = 'idx_rule');
SET @t3 := IF(@i3 = 0,
  'ALTER TABLE cert_expert_task_data_gap ADD KEY `idx_rule` (`RuleCode`)',
  'SELECT ''idx_rule 已存在，跳过'' AS msg');
PREPARE y3 FROM @t3; EXECUTE y3; DEALLOCATE PREPARE y3;

-- ────────────────────────────────────────────────────────────
-- 5. ★ J1：GapKey 重定义
--    旧：CONCAT(GapType,'|',FieldCode,'|',TableCode,'|',SourceItemCode)   ← 规则级
--    新：CONCAT(GapType,'|',RuleCode,'|',FieldCode,'|',TableCode)          ← 字段级
--
--    ⚠️ 必须在【首次产生 gap 之前】执行。该表实测 0 行 ⇒ 零迁移成本。
--       若已有数据，先查重复：
--         SELECT TaskCode, GapType, RuleCode, FieldCode, TableCode, COUNT(*) c
--           FROM cert_expert_task_data_gap GROUP BY 1,2,3,4,5 HAVING c > 1;
-- ────────────────────────────────────────────────────────────
SELECT '★ 重复检查（应为空）' AS guard;
SELECT TaskCode, GapType, RuleCode, FieldCode, TableCode, COUNT(*) AS c
  FROM cert_expert_task_data_gap
 GROUP BY TaskCode, GapType, RuleCode, FieldCode, TableCode
HAVING c > 1;

-- ⚠️ 此处【直接执行 ALTER，不包在 PREPARE 里】：
--    生成列表达式里的 IFNULL(...,'-') 与 COMMENT 里的单引号需要双重转义，
--    放进 SET @var := '...' 会因转义层数过多而语法错误（2026-09-30 实测踩过）。
--    判存由上面的"重复检查"承担：本脚本仅在首次产生 gap 之前运行一次。
ALTER TABLE cert_expert_task_data_gap
  DROP INDEX `uk_gap`,
  DROP COLUMN `GapKey`,
  ADD COLUMN `GapKey` varchar(300) COLLATE utf8mb4_general_ci
    GENERATED ALWAYS AS (
      CONCAT(`GapType`, '|', IFNULL(`RuleCode`, '-'), '|', IFNULL(`FieldCode`, '-'), '|', IFNULL(`TableCode`, '-'))
    ) STORED
    COMMENT '★去重键（25 号 J1：字段级粒度；RuleCode/FieldCode 用 - 占位，避免 NULL 逃逸唯一性判定）',
  ADD UNIQUE KEY `uk_gap` (`TaskCode`, `GapKey`);

-- ════════════════════════════════════════════════════════════
-- 验证
-- ⚠️ information_schema.COLUMN_NAME 排序规则大小写不敏感，
--    必须 CONVERT(... USING utf8mb4) COLLATE utf8mb4_bin，否则会假阴性。
-- ════════════════════════════════════════════════════════════

SELECT CONVERT(TABLE_NAME USING utf8mb4) COLLATE utf8mb4_bin AS Tbl,
       CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin AS Col,
       COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND ((CONVERT(TABLE_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'cert_extraction_result'
         AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('ValueSource','IsManualEdited'))
     OR (CONVERT(TABLE_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'cert_table_extraction_result'
         AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('ValueSource','IsManualEdited'))
     OR (CONVERT(TABLE_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'cert_expert_task_data_gap'
         AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('RuleCode','GapKey')))
 ORDER BY Tbl, Col;

-- 存量行应全为 auto（零回填验证）
SELECT 'cert_extraction_result' AS Tbl, ValueSource, COUNT(*) AS n
  FROM cert_extraction_result GROUP BY ValueSource
UNION ALL
SELECT 'cert_table_extraction_result', ValueSource, COUNT(*)
  FROM cert_table_extraction_result GROUP BY ValueSource;

-- GapKey 生成列自检
SELECT CONCAT(GENERATION_EXPRESSION, '') AS GapKeyExpr
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND CONVERT(TABLE_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'cert_expert_task_data_gap'
   AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'GapKey';

-- 索引自检
SELECT CONVERT(TABLE_NAME USING utf8mb4) COLLATE utf8mb4_bin AS Tbl,
       INDEX_NAME,
       GROUP_CONCAT(CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin
                    ORDER BY SEQ_IN_INDEX SEPARATOR ',') AS Cols
  FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA = DATABASE()
   AND INDEX_NAME IN ('idx_ent_rule_field','idx_ent_rule_table','idx_rule','uk_gap')
 GROUP BY TABLE_NAME, INDEX_NAME;
