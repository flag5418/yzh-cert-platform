-- =====================================================================
-- 20261008_validation_rule_isvalid_wfcode_V1.sql
--
-- 目的：cert_validation_rule（NC 检查规则）两项 P1 收口
--   ① 启停字段 IsActive(bool) → IsValid(int 0/1)     —— 铁律九，与其余 8 张配置表判据统一
--   ② 删除 WorkflowCode 僵尸列 + 解 FK                —— DAG 已由本表 RuleJson 承载
--
-- ★ 执行顺序（务必）：先编译并重启后端（新实体已无 WorkflowCode / IsActive），再跑本脚本。
--   反序执行会出现「实体仍映射已删列 ⇒ SELECT 报 Unknown column」。
--
-- 背景（实库实测 2026-10-08）：
--   · IsValid tinyint NULL（两行均为 1，由 fix-stage-code-align-2026-09-30.sql 补齐）
--   · IsActive tinyint(1) DEFAULT 1 与 IsValid 并存 → 违反铁律九「启用/禁用唯一字段」
--   · WorkflowCode varchar(36) NULL，两行均为 NULL，但 FK fk_valrule_workflow
--     仍指向 wf_workflow_definition —— 该表 0 行（20260922 已判死）；
--     任何非空写入将触发 ERROR 1452 ⇒ 必须解 FK 再删列
--
-- 遵守铁律：
--   铁律七  列名 PascalCase（本脚本不新增列）
--   铁律八  全库统一 utf8mb4_general_ci
--   铁律九  禁 Enable/IsActive，启停唯一字段 = IsValid
--   准则 A  Id 不作业务键（本脚本不触碰 Id）
--
-- 幂等：所有 DDL 先查 information_schema 再 PREPARE/EXECUTE
--   ⚠️ COLUMN_NAME 比对必须用 BINARY —— information_schema 的排序规则大小写不敏感，
--      直接 = 'IsActive' 会同时命中 isactive（假阴性陷阱，见 fix-column-naming-2026-09-24.sql）
-- =====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
USE yzh_cert_platform;

SET @db := DATABASE();

-- ────────────────────────────────────────────────────────────
-- 0) 备份（可回滚）
-- ────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `_bak_20261008_cert_validation_rule`
AS SELECT * FROM `cert_validation_rule`;

SELECT '备份完成' AS step, COUNT(*) AS rows_backed_up
  FROM `_bak_20261008_cert_validation_rule`;

-- ────────────────────────────────────────────────────────────
-- 1) IsValid 回填：以 IsActive 为真源，NULL 兜底为 1（启用）
--    必须在 DROP COLUMN IsActive 之前执行
-- ────────────────────────────────────────────────────────────
UPDATE `cert_validation_rule`
   SET `IsValid` = IF(COALESCE(`IsActive`, 1) = 1, 1, 0)
 WHERE `IsValid` IS NULL
    OR `IsValid` <> IF(COALESCE(`IsActive`, 1) = 1, 1, 0);

-- 校验：应 0 行
SELECT CONCAT('IsValid/IsActive 不一致: ', COUNT(*)) AS violation
  FROM `cert_validation_rule`
 WHERE `IsValid` <> IF(COALESCE(`IsActive`, 1) = 1, 1, 0);

-- ────────────────────────────────────────────────────────────
-- 2) 解 FK（必须先于 DROP COLUMN）
-- ────────────────────────────────────────────────────────────
SET @fk := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
             WHERE CONSTRAINT_SCHEMA = @db
               AND TABLE_NAME = 'cert_validation_rule'
               AND CONSTRAINT_NAME = 'fk_valrule_workflow'
               AND CONSTRAINT_TYPE = 'FOREIGN KEY');
SET @sql := IF(@fk > 0,
  'ALTER TABLE `cert_validation_rule` DROP FOREIGN KEY `fk_valrule_workflow`',
  'SELECT ''fk_valrule_workflow 不存在，跳过'' AS msg');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ────────────────────────────────────────────────────────────
-- 3) 删 WorkflowCode 僵尸列（其单列索引随列自动移除；先显式删索引以防多列索引残留）
-- ────────────────────────────────────────────────────────────
SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
              WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
                AND INDEX_NAME = 'idx_workflow_code');
SET @sql := IF(@idx > 0,
  'ALTER TABLE `cert_validation_rule` DROP INDEX `idx_workflow_code`',
  'SELECT ''idx_workflow_code 不存在，跳过'' AS msg');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
                AND BINARY COLUMN_NAME = 'WorkflowCode');
SET @sql := IF(@col > 0,
  'ALTER TABLE `cert_validation_rule` DROP COLUMN `WorkflowCode`',
  'SELECT ''WorkflowCode 列不存在，跳过'' AS msg');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ────────────────────────────────────────────────────────────
-- 4) 删 IsActive（回填已完成 ⇒ 可安全删除；启停唯一字段从此为 IsValid）
-- ────────────────────────────────────────────────────────────
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
                AND BINARY COLUMN_NAME = 'IsActive');
SET @sql := IF(@col > 0,
  'ALTER TABLE `cert_validation_rule` DROP COLUMN `IsActive`',
  'SELECT ''IsActive 列不存在，跳过'' AS msg');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ────────────────────────────────────────────────────────────
-- 5) IsValid 落为 NOT NULL DEFAULT 1（与 ReportSection 对称）
-- ────────────────────────────────────────────────────────────
SET @nullable := (SELECT IS_NULLABLE FROM information_schema.COLUMNS
                   WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
                     AND BINARY COLUMN_NAME = 'IsValid');
SET @sql := IF(@nullable = 'YES',
  'ALTER TABLE `cert_validation_rule` MODIFY COLUMN `IsValid` tinyint NOT NULL DEFAULT 1 COMMENT ''有效标志（1=有效，0=无效）—— 铁律九唯一启用字段''',
  'SELECT ''IsValid 已是 NOT NULL，跳过'' AS msg');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- =====================================================================
-- 6) 验证
-- =====================================================================
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
 ORDER BY ORDINAL_POSITION;

-- 应 0 行：不得再有 WorkflowCode / IsActive
SELECT CONCAT('残留待删列: ', COLUMN_NAME) AS violation
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
   AND BINARY COLUMN_NAME IN ('WorkflowCode', 'IsActive');

-- 应 0 行：不得再有指向 wf_workflow_definition 的 FK
SELECT CONCAT('残留死 FK: ', CONSTRAINT_NAME, ' → ', REFERENCED_TABLE_NAME) AS violation
  FROM information_schema.KEY_COLUMN_USAGE
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
   AND REFERENCED_TABLE_NAME = 'wf_workflow_definition';

-- 应 0 行：不得再有指向 cert_phase_definition（已 DROP）的残留 FK
SELECT CONCAT('残留阶段 FK: ', CONSTRAINT_NAME, ' → ', REFERENCED_TABLE_NAME) AS violation
  FROM information_schema.KEY_COLUMN_USAGE
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
   AND REFERENCED_TABLE_NAME = 'cert_phase_definition';

-- 列名大小写体检（应 0 行）：任何非 PascalCase 列名即违规
SELECT CONCAT('命名违规列: ', COLUMN_NAME) AS violation
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
   AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z][A-Za-z0-9]*$';

-- 字符集体检（应 0 行）
SELECT CONCAT('字符集违规列: ', COLUMN_NAME, ' = ', COLLATION_NAME) AS violation
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_validation_rule'
   AND COLLATION_NAME IS NOT NULL
   AND COLLATION_NAME <> 'utf8mb4_general_ci';

-- 规则行现状（IsValid 应全部为 0/1，无 NULL）
SELECT `Code`, `RuleCode`, `RuleName`, `IsValid`,
       CASE WHEN `RuleJson` IS NULL THEN 'NULL(未配DAG)' ELSE '有DAG' END AS DAG
  FROM `cert_validation_rule`
 ORDER BY `RuleCode`;
