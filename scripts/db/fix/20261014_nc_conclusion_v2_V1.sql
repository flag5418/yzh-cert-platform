-- =============================================================================
-- 2026-10-14 NC 结论判定 v2（多 judge + 级别配置）· DDL + Skill 登记
-- -----------------------------------------------------------------------------
-- 背景（2026-10-14 用户裁决）：
--   ① NC 检查由多个【确定性 nc_judge 节点】产出，各自盖「级别」（是否影响结论）
--   ② 经 collect_judges 聚合 → nc_conclusion_v2（★纯代码决策矩阵·AI 不参与）算权威结论
--   ③ 决策矩阵 = 专家配置（哪些级别算核心 + 升级阈值 + 容忍数），落 cert_conclusion_rule
--      新增 3 列：CoreLevels / MinorTolerated / ConclusionLabels
--      （MinorRejectThreshold 复用已有列，作为核心级 fail 的「升级复审阈值」）
--   ④ 3 个新 skill：stamp_judge_level（盖级别）/ collect_judges（聚合）/ nc_conclusion_v2（算结论）
--
-- 【铁律】
--   七（列名 PascalCase 三处一致）/ 八（显式 COLLATE utf8mb4_general_ci）
--   九（Enable 零容忍，唯一启用字段 IsValid）★ 本脚本绝不新增 Enable/enable 列
--
-- 【幂等】
--   · ALTER 用存储过程判列存在（参照 phase10 范式）
--   · wf_skill / wf_skill_reflection 用 ON DUPLICATE KEY UPDATE
-- =============================================================================
USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- 0. 幂等辅助存储过程（判列/判索引存在）
-- -----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS `p_ncv2_add_column`;
DELIMITER //
CREATE PROCEDURE `p_ncv2_add_column`(IN p_table VARCHAR(64), IN p_col VARCHAR(64), IN p_def VARCHAR(500))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS
                 WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_col) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` ADD COLUMN `', p_col, '` ', p_def);
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Added ', p_table, '.', p_col) AS result;
  ELSE
    SELECT CONCAT('Skipped: ', p_table, '.', p_col, ' exists') AS result;
  END IF;
END//
DELIMITER ;

-- -----------------------------------------------------------------------------
-- 1. cert_conclusion_rule 新增 3 列（v2 决策矩阵配置）
-- -----------------------------------------------------------------------------
CALL p_ncv2_add_column('cert_conclusion_rule', 'CoreLevels',
  "json NULL COMMENT '★v2 哪些级别算「核心·影响结论」（JSON 数组，如 [\"major\",\"minor\"]；NULL=缺省 [major,minor]；critical 恒并入核心）'");
CALL p_ncv2_add_column('cert_conclusion_rule', 'MinorTolerated',
  "int NOT NULL DEFAULT -1 COMMENT '★v2 非核心(observation) fail 容忍数；-1=不限量（默认，2 条轻微不过仍可推荐认证）'");
CALL p_ncv2_add_column('cert_conclusion_rule', 'ConclusionLabels',
  "json NULL COMMENT '★v2 结论词映射（JSON：pass/pass_with_nc/improvement_required/fail → 中文词；NULL=用内置词）'");

DROP PROCEDURE IF EXISTS `p_ncv2_add_column`;

-- -----------------------------------------------------------------------------
-- 2. wf_skill 主表：登记 3 个新 skill（ON DUPLICATE KEY UPDATE，依赖 uk Code）
--    ★ 照抄 nc_conclusion_calc 范式：SkillType=manual / CategoryCode=ai_judge / IsUseForNcConfig=1
--    CategoryCode 用数据里现有的分类编码（ai_judge）
-- -----------------------------------------------------------------------------
INSERT INTO `wf_skill`
  (`Code`, `Name`, `SkillType`, `CategoryCode`, `SideEffect`, `IsActive`, `OutputStrict`, `ReturnType`, `IsUseForNcConfig`, `IsUseForReport`, `IsDeleted`, `IsValid`, `CreateTime`, `CreateBy`)
VALUES
  ('stamp_judge_level', 'judge盖章级别', 'manual', 'ai_judge', 0, 1, 1, 'json', 1, 0, 0, 1, NOW(), 'system_init'),
  ('collect_judges',    '聚合多路judge',  'manual', 'ai_judge', 0, 1, 1, 'json', 1, 0, 0, 1, NOW(), 'system_init'),
  ('nc_conclusion_v2',  'NC结论判定v2(多judge)', 'manual', 'ai_judge', 0, 1, 1, 'json', 1, 0, 0, 1, NOW(), 'system_init')
ON DUPLICATE KEY UPDATE
  `Name`             = VALUES(`Name`),
  `SkillType`        = 'manual',
  `IsUseForNcConfig` = 1,
  `IsActive`         = 1,
  `IsValid`          = 1;

-- -----------------------------------------------------------------------------
-- 3. wf_skill_reflection：反射地址（classPath + methodName，CertSkillRegistry 按 SkillCode 查）
-- -----------------------------------------------------------------------------
INSERT INTO `wf_skill_reflection`
  (`Code`, `SkillCode`, `ClassPath`, `MethodName`, `ParamBinding`, `Status`, `IsDeleted`, `IsValid`, `CreateTime`, `CreateBy`)
VALUES
  (REPLACE(UUID(), '-', ''), 'stamp_judge_level',
   'CertPlatform.Admin.Services.Workflow.Skills.StampJudgeLevelSkill', 'ExecuteAsync', NULL, 'active', 0, 1, NOW(), 'system_init'),
  (REPLACE(UUID(), '-', ''), 'collect_judges',
   'CertPlatform.Admin.Services.Workflow.Skills.CollectJudgesSkill', 'ExecuteAsync', NULL, 'active', 0, 1, NOW(), 'system_init'),
  (REPLACE(UUID(), '-', ''), 'nc_conclusion_v2',
   'CertPlatform.Admin.Services.Workflow.Skills.NcConclusionV2Skill', 'ExecuteAsync', NULL, 'active', 0, 1, NOW(), 'system_init')
ON DUPLICATE KEY UPDATE
  `ClassPath`   = VALUES(`ClassPath`),
  `MethodName`  = VALUES(`MethodName`),
  `IsValid`     = 1,
  `IsDeleted`   = 0,
  `Status`      = 'active',
  `UpdateTime`  = NOW();

-- -----------------------------------------------------------------------------
-- 4. 种子：把现有平台通用规则（CR-GENERIC-0001）补上 v2 决策矩阵占位值
--    ★ 仍为占位值（SourceRef 已标注），机构确认后改配置，不改代码
-- -----------------------------------------------------------------------------
UPDATE `cert_conclusion_rule`
SET `CoreLevels`       = JSON_ARRAY('major', 'minor'),
    `MinorTolerated`   = -1,
    `ConclusionLabels` = JSON_OBJECT(
        'pass', '通过（推荐认证）',
        'pass_with_nc', '通过（带不符合项）',
        'improvement_required', '待改进后复审',
        'fail', '不通过'),
    `UpdateTime`       = NOW()
WHERE `Code` = 'CR-GENERIC-0001';

-- -----------------------------------------------------------------------------
-- 验证（执行后人工确认）
-- -----------------------------------------------------------------------------
SELECT '① 三列已建' AS 检查项, COUNT(*) AS 实际, 3 AS 期望 FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_conclusion_rule'
  AND COLUMN_NAME IN ('CoreLevels','MinorTolerated','ConclusionLabels')
UNION ALL
SELECT '② 铁律九:无 Enable', COUNT(*), 0 FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_conclusion_rule'
  AND COLUMN_NAME IN ('Enable','enable')
UNION ALL
SELECT '③ 非 PascalCase 列数(应=0)', COUNT(*), 0 FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_conclusion_rule'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]'
UNION ALL
SELECT '④ 新 skill 登记数', COUNT(*), 3 FROM wf_skill WHERE Code IN ('stamp_judge_level','collect_judges','nc_conclusion_v2') AND IsDeleted=0
UNION ALL
SELECT '⑤ 反射登记数', COUNT(*), 3 FROM wf_skill_reflection WHERE SkillCode IN ('stamp_judge_level','collect_judges','nc_conclusion_v2') AND IsDeleted=0
UNION ALL
SELECT '⑥ v2 矩阵种子', COUNT(*), 1 FROM cert_conclusion_rule WHERE Code='CR-GENERIC-0001' AND CoreLevels IS NOT NULL;
-- 期望：①3 ②0 ③1 ④3 ⑤3 ⑥1
