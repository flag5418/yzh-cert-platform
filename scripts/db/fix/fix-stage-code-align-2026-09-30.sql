-- ============================================================================
-- fix-stage-code-align-2026-09-30.sql
-- 目的：① 修 cert_validation_rule.IsValid 全为 NULL（规则查不出来）
--       ② 统一阶段编码口径：配置层 PhaseCode 由【业务码】改为【GUID】
--          （GUID = cert_cert_stage.Code，与 cert_enterprise_stage / 目录配置 / 任务表一致）
--       ③ 给两张配置表的 PhaseCode 补 FK → cert_cert_stage.Code
-- 依据：docs/40-实施/专家任务设计/06-数据库设计-V1.md §4.1
--       docs/40-实施/专家任务设计/22-任务建立实施前置与草图-V1.md §1.1
-- ⚠️ 幂等：可重复执行。
-- ⚠️ 实测（2026-09-30）：
--      cert_validation_rule 2 行，PhaseCode='03'，IsValid=NULL
--      cert_report_section  1 行，PhaseCode='03'，IsValid=1
--      cert_cert_stage      StageCode='03' → Code='29c1bcc3a18942e1865b2497a0262504'（复审）
-- ⛔ 若不修 ①：SqlSugarDbOrm.IsValidCondition<T>() 自动加 WHERE IsValid=1
--    ⇒ 2 条规则【永远查不出来】⇒ ResolveScope 返回 0 行 ⇒ 静默建出「0 检查项」空任务
-- ⛔ 若不修 ②：ResolveScope 拿 GUID 查 PhaseCode ⇒ 同样 0 行 ⇒ 同上
-- ============================================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SELECT '========== 0. 迁移前现状 ==========' AS `step`;

SELECT '-- 0.1 规则表 --' AS `check`;
SELECT vr.Code, vr.RuleCode, vr.PhaseCode AS `当前值`, vr.IsValid,
       cs.Code AS `目标GUID`, cs.StageName
FROM cert_validation_rule vr
LEFT JOIN cert_cert_stage cs ON cs.StageCode = vr.PhaseCode
WHERE vr.IsDeleted = 0;

SELECT '-- 0.2 章节表 --' AS `check`;
SELECT s.Code, s.PhaseCode AS `当前值`, s.IsValid,
       cs.Code AS `目标GUID`, cs.StageName
FROM cert_report_section s
LEFT JOIN cert_cert_stage cs ON cs.StageCode = s.PhaseCode
WHERE s.IsDeleted = 0;

-- ============================================================================
-- 1. ★ 修 IsValid 全为 NULL（本项是所有后续工作的前提）
-- ============================================================================
SELECT '========== 1. 修 cert_validation_rule.IsValid ==========' AS `step`;

UPDATE cert_validation_rule
   SET IsValid = 1
 WHERE IsValid IS NULL;

SELECT ROW_COUNT() AS `受影响行数（期望 2）`;

SELECT '-- 验收：应为 0 --' AS `check`;
SELECT COUNT(*) AS `IsValid 仍为 NULL 的行数`
FROM cert_validation_rule WHERE IsValid IS NULL;

-- ============================================================================
-- 2. ★ 阶段码迁移：业务码 → GUID
-- ============================================================================
SELECT '========== 2. 阶段码迁移 业务码 → GUID ==========' AS `step`;

-- 2.1 cert_validation_rule
UPDATE cert_validation_rule vr
INNER JOIN cert_cert_stage cs ON cs.StageCode = vr.PhaseCode
   SET vr.PhaseCode   = cs.Code,
       vr.UpdateBy    = 'fix_stage_align_20260930',
       vr.UpdateTime  = NOW()
 WHERE vr.PhaseCode IS NOT NULL
   AND vr.PhaseCode <> cs.Code;

SELECT ROW_COUNT() AS `规则表受影响行数（期望 2）`;

-- 2.2 cert_report_section
UPDATE cert_report_section s
INNER JOIN cert_cert_stage cs ON cs.StageCode = s.PhaseCode
   SET s.PhaseCode   = cs.Code,
       s.UpdateBy    = 'fix_stage_align_20260930',
       s.UpdateTime  = NOW()
 WHERE s.PhaseCode IS NOT NULL
   AND s.PhaseCode <> cs.Code;

SELECT ROW_COUNT() AS `章节表受影响行数（期望 1）`;

-- 2.3 查漏网（迁移后仍对不上 cert_cert_stage.Code 的脏数据）—— 期望 0 行
SELECT '-- 2.3 漏网检查（期望 0 行）--' AS `check`;
SELECT 'cert_validation_rule' AS `表`, vr.Code, vr.PhaseCode
FROM cert_validation_rule vr
LEFT JOIN cert_cert_stage cs ON cs.Code = vr.PhaseCode
WHERE vr.PhaseCode IS NOT NULL AND cs.Code IS NULL AND vr.IsDeleted = 0
UNION ALL
SELECT 'cert_report_section', s.Code, s.PhaseCode
FROM cert_report_section s
LEFT JOIN cert_cert_stage cs ON cs.Code = s.PhaseCode
WHERE s.PhaseCode IS NOT NULL AND cs.Code IS NULL AND s.IsDeleted = 0;

-- 2.4 迁移后验收
SELECT '-- 2.4 迁移后（期望 StageName=复审）--' AS `check`;
SELECT vr.RuleCode, vr.PhaseCode AS `GUID`, cs.StageName
FROM cert_validation_rule vr
INNER JOIN cert_cert_stage cs ON cs.Code = vr.PhaseCode;

SELECT s.Code AS `SectionCode`, s.PhaseCode AS `GUID`, cs.StageName
FROM cert_report_section s
INNER JOIN cert_cert_stage cs ON cs.Code = s.PhaseCode;

-- ============================================================================
-- 3. 补 FK（★ 实测原表 PhaseCode 无 FK，故直接 ADD，无需 DROP）
-- ============================================================================
SELECT '========== 3. 补 FK ==========' AS `step`;

SELECT '-- 3.1 补前确认（期望 0 行）--' AS `check`;
SELECT TABLE_NAME, CONSTRAINT_NAME, COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_validation_rule', 'cert_report_section')
  AND COLUMN_NAME = 'PhaseCode'
  AND REFERENCED_TABLE_NAME IS NOT NULL;

-- 3.2 动态补 FK（幂等：已存在则跳过，避免 ERROR 1826 Duplicate foreign key）
SET @fk1 := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
             WHERE CONSTRAINT_SCHEMA = DATABASE()
               AND TABLE_NAME = 'cert_validation_rule'
               AND CONSTRAINT_NAME = 'fk_valrule_stage');
SET @sql1 := IF(@fk1 = 0,
  'ALTER TABLE `cert_validation_rule` ADD CONSTRAINT `fk_valrule_stage` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_cert_stage` (`Code`) ON DELETE RESTRICT ON UPDATE CASCADE',
  'SELECT ''fk_valrule_stage 已存在，跳过'' AS `skip`');
PREPARE s1 FROM @sql1; EXECUTE s1; DEALLOCATE PREPARE s1;

SET @fk2 := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
             WHERE CONSTRAINT_SCHEMA = DATABASE()
               AND TABLE_NAME = 'cert_report_section'
               AND CONSTRAINT_NAME = 'fk_rptsec_stage');
SET @sql2 := IF(@fk2 = 0,
  'ALTER TABLE `cert_report_section` ADD CONSTRAINT `fk_rptsec_stage` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_cert_stage` (`Code`) ON DELETE RESTRICT ON UPDATE CASCADE',
  'SELECT ''fk_rptsec_stage 已存在，跳过'' AS `skip`');
PREPARE s2 FROM @sql2; EXECUTE s2; DEALLOCATE PREPARE s2;

-- 3.3 补后验收（期望 2 行）
SELECT '-- 3.3 补后 FK 清单（期望 2 行）--' AS `check`;
SELECT TABLE_NAME, CONSTRAINT_NAME, COLUMN_NAME,
       REFERENCED_TABLE_NAME, REFERENCED_COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_validation_rule', 'cert_report_section')
  AND COLUMN_NAME = 'PhaseCode'
  AND REFERENCED_TABLE_NAME IS NOT NULL;

-- ============================================================================
-- 4. 记录 cert_org_stage 双码并存的既定事实（★ 本期不改）
--    cert_org_stage.StageCode        → cert_cert_stage.StageCode（业务码 jd01/jd02/03）
--    cert_enterprise_stage.StageCode → cert_cert_stage.Code（GUID）
--    cert_expert_*.StageCode         → cert_cert_stage.Code（GUID）★ 新增表一律 GUID
-- ============================================================================
SELECT '========== 4. 双码并存现状（仅记录，不改） ==========' AS `step`;
SELECT 'cert_org_stage' AS `表`, os.StageCode AS `存的值`, cs.StageName,
       CASE WHEN cs.StageCode = os.StageCode THEN '业务码口径' ELSE 'GUID口径' END AS `口径`
FROM cert_org_stage os
LEFT JOIN cert_cert_stage cs ON cs.StageCode = os.StageCode OR cs.Code = os.StageCode;

SELECT 'cert_enterprise_stage' AS `表`, COUNT(*) AS `行数`,
       CASE WHEN cs.Code IS NOT NULL THEN 'GUID口径' ELSE '未知' END AS `口径`
FROM cert_enterprise_stage es
LEFT JOIN cert_cert_stage cs ON cs.Code = es.StageCode
GROUP BY `口径`;

SELECT '========== 修复完成 ==========' AS `step`;
