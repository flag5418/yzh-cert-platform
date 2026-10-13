-- ============================================================================
-- 归属模型重设计 V2 —— 数据迁移 + DDL
-- 日期：2026-10-13
-- 依据：docs/40-实施/迁移计划/归属模型重设计-实施-V1.md §六 步骤 1
-- 承接：归属判据收敛与机构专用列-设计-V1.md（commit 3d5bb05c，OwnerOrgCode 已落库）
-- ----------------------------------------------------------------------------
-- 模型（10-13 用户澄清版）：
--   OrgCode        恒 = 机构（= sys_organization.Code / cert_certification_body.Code）
--   EnterpriseCode 恒 = 企业（唯一分类判据：非空 ⇒ 企业行）
--   平台标准级     = 两列都空  ⇒ 消灭 magic value 'yzh-std-env'
--   ★ 企业行可同时带 OrgCode = 机构 code（冗余，用于快速取后台规则/标准）⇒ 合法
-- ----------------------------------------------------------------------------
-- 实测规模：175 行 / 12 表（视图 v_iso_standard 跟随基表，不单独处理）
-- ⚠️ 本脚本与「代码改造」必须同一次停机窗口内连续完成（中间态不可用）
-- ⚠️ DDL 隐式提交，不可回滚 ⇒ 反向依赖 §1 快照表 _bak_20261013_owner_v2
-- ============================================================================

-- ---------- §0 前置检查（执行前人工核对，不改数据） ----------
-- SELECT COUNT(*) FROM cert_standard_directory_config;      -- 期望 2
-- SELECT COUNT(*) FROM cert_extraction_result;              -- 期望 0
-- SELECT COUNT(*) FROM cert_table_extraction_result;        -- 期望 0
-- SELECT COUNT(*) FROM cert_standard_directory_file;        -- 期望 23
-- SELECT COUNT(*) FROM cert_standard_directory_file WHERE EnterpriseCode = '';  -- 期望 23

-- ---------- §1 快照（反向脚本依赖，必须先执行） ----------
DROP TABLE IF EXISTS `_bak_20261013_owner_v2`;
CREATE TABLE `_bak_20261013_owner_v2` (
  `SrcTable` varchar(64) NOT NULL,
  `Id`       bigint      NOT NULL,
  PRIMARY KEY (`SrcTable`, `Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='归属模型V2迁移快照（OrgCode=yzh-std-env 的行）';

INSERT INTO `_bak_20261013_owner_v2` (`SrcTable`, `Id`)
SELECT 'cert_iso_clause',                `Id` FROM `cert_iso_clause`                WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_fill_param_def',           `Id` FROM `cert_fill_param_def`           WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_report_section',           `Id` FROM `cert_report_section`           WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_validation_rule',          `Id` FROM `cert_validation_rule`          WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_doc_fill_prompt',          `Id` FROM `cert_doc_fill_prompt`          WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_iso_standard',             `Id` FROM `cert_iso_standard`             WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_ai_config',                `Id` FROM `cert_ai_config`                WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_conclusion_rule',          `Id` FROM `cert_conclusion_rule`          WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_nc_conclude_prompt',       `Id` FROM `cert_nc_conclude_prompt`       WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_nc_judge_prompt',          `Id` FROM `cert_nc_judge_prompt`          WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'wf_prompt_template',            `Id` FROM `wf_prompt_template`            WHERE `OrgCode` = 'yzh-std-env'
UNION ALL SELECT 'cert_standard_directory_config',`Id` FROM `cert_standard_directory_config` WHERE `OrgCode` = 'yzh-std-env';

-- 期望：175 行（= 11 表 175 行；_config 2 行另计）
-- SELECT SrcTable, COUNT(*) FROM _bak_20261013_owner_v2 GROUP BY SrcTable;

-- ---------- §2 数据迁移：平台标准级 ⇒ 两列都空（11 表，⛔ 不含 _config） ----------
-- ⚠️ _config 的 OrgCode 走 §3（要回填成机构，不是置空）

UPDATE `cert_iso_clause`                 SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_fill_param_def`             SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_report_section`             SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_validation_rule`            SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_doc_fill_prompt`            SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_iso_standard`               SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_ai_config`                  SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_conclusion_rule`            SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_nc_conclude_prompt`         SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `cert_nc_judge_prompt`            SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';
UPDATE `wf_prompt_template`              SET `OrgCode` = '' WHERE `OrgCode` = 'yzh-std-env';

-- ---------- §3 cert_standard_directory_config：机构维度回落到 OrgCode，删 OwnerOrgCode ----------

-- 3.1 回填：OwnerOrgCode（本批刚加的机构专用列）⇒ OrgCode（新模型下它就是机构列）
--     b27945be…：OwnerOrgCode = 906e8b2a… ⇒ OrgCode = 906e8b2a…（机构模板；23 行文件路径不变）
--     464d1b41…：OwnerOrgCode = ''       ⇒ OrgCode = ''       （平台通用模板；0 文件）
UPDATE `cert_standard_directory_config` SET `OrgCode` = `OwnerOrgCode` WHERE `OrgCode` = 'yzh-std-env';

-- 3.2 换唯一键：uk_owner_std_stage(OwnerOrgCode,…) ⇒ uk_org_ent_std_stage(OrgCode,…)
ALTER TABLE `cert_standard_directory_config` DROP INDEX `uk_owner_std_stage`;
ALTER TABLE `cert_standard_directory_config`
  ADD UNIQUE KEY `uk_org_ent_std_stage` (`OrgCode`, `EnterpriseCode`, `StandardCode`, `StageCode`);

-- 3.3 删机构专用列（新模型下 OrgCode 已承载机构 ⇒ 该列多余）
ALTER TABLE `cert_standard_directory_config` DROP COLUMN `OwnerOrgCode`;

-- 3.4 列注释重写
ALTER TABLE `cert_standard_directory_config`
  MODIFY COLUMN `OrgCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL
    COMMENT '机构 Code（= sys_organization.Code / cert_certification_body.Code）；空 = 平台通用模板（不特指机构）。★ 判据唯一入口 OrgScope',
  MODIFY COLUMN `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL
    COMMENT '企业 Code。真实值 = 该企业该标准该阶段的企业目录；空 = 不属于任何企业（机构/平台模板行）。★ 判据唯一入口 OrgScope.IsEnterpriseRow';

-- ---------- §4 提取结果表：DB 列名归位（OrgCode ⇒ EnterpriseCode） ----------
-- 背景：两表的 DB 列名是历史遗留（列名 OrgCode，语义/注释/C# 属性名都是「企业」）
--       ⇒ 改名后 C# 侧只需去掉 [SugarColumn(ColumnName="OrgCode")]，读侧零改动
-- ⚠️ 两表各 3 个索引以该列打头，CHANGE COLUMN 会自动跟随更新
-- ⚠️ 0 行，零数据成本

ALTER TABLE `cert_extraction_result`
  CHANGE COLUMN `OrgCode` `EnterpriseCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL
    COMMENT '企业 Code（本表归属维度 = 企业）';

ALTER TABLE `cert_table_extraction_result`
  CHANGE COLUMN `OrgCode` `EnterpriseCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL
    COMMENT '企业 Code（本表归属维度 = 企业）';

-- ---------- §5 注释统一（11 表的 OrgCode 列） ----------
ALTER TABLE `cert_iso_clause`      MODIFY COLUMN `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构 Code；空 = 平台标准级（全平台共享，如 ISO 条款）';
ALTER TABLE `cert_iso_standard`    MODIFY COLUMN `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构 Code；空 = 平台标准级';
ALTER TABLE `cert_ai_config`       MODIFY COLUMN `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构 Code；空 = 平台标准级';
ALTER TABLE `cert_validation_rule` MODIFY COLUMN `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构 Code；空 = 平台标准级';
ALTER TABLE `cert_report_section`  MODIFY COLUMN `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构 Code（配置层归属）；空 = 平台标准级';
ALTER TABLE `wf_prompt_template`   MODIFY COLUMN `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构 Code；空 = 平台标准级';

-- ---------- §6 验证（期望值见行尾） ----------

-- 6a. 全库不得再有 yzh-std-env（_bak 快照除外）
SELECT 'cert_iso_clause' t, COUNT(*) n FROM cert_iso_clause                 WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_fill_param_def',      COUNT(*) FROM cert_fill_param_def             WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_report_section',      COUNT(*) FROM cert_report_section             WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_validation_rule',     COUNT(*) FROM cert_validation_rule            WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_doc_fill_prompt',     COUNT(*) FROM cert_doc_fill_prompt            WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_iso_standard',        COUNT(*) FROM cert_iso_standard               WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_ai_config',           COUNT(*) FROM cert_ai_config                  WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_conclusion_rule',     COUNT(*) FROM cert_conclusion_rule            WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_nc_conclude_prompt',  COUNT(*) FROM cert_nc_conclude_prompt         WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_nc_judge_prompt',     COUNT(*) FROM cert_nc_judge_prompt            WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'wf_prompt_template',       COUNT(*) FROM wf_prompt_template              WHERE OrgCode='yzh-std-env'
UNION ALL SELECT 'cert_standard_directory_config', COUNT(*) FROM cert_standard_directory_config WHERE OrgCode='yzh-std-env';
-- 期望：全部 0

-- 6b. _config 两行新形态（期望：b27945be… = 906e8b2a… / 464d1b41… = ''）
SELECT Code, OrgCode, EnterpriseCode, StandardCode, StageCode FROM cert_standard_directory_config;

-- 6c. 存量 23 行文件路径不变（期望：/standard-directory/906e8b2a…/…）
SELECT Code, EnterpriseCode, StoragePath FROM cert_standard_directory_file LIMIT 3;

-- 6d. 提取结果表列名归位（期望：EnterpriseCode 存在、OrgCode 不存在）
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME IN ('cert_extraction_result','cert_table_extraction_result')
  AND COLUMN_NAME IN ('OrgCode','EnterpriseCode');

-- 6e. _config 索引（期望：uk_org_ent_std_stage + idx_org_code；无 uk_owner_std_stage）
SELECT INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_standard_directory_config'
GROUP BY INDEX_NAME;

-- ============================================================================
-- §7 反向脚本（仅在需要回退时执行；顺序不可颠倒）
-- ============================================================================
-- -- 7.1 提取结果表列名改回
-- ALTER TABLE `cert_extraction_result`
--   CHANGE COLUMN `EnterpriseCode` `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '企业 Code（本表归属维度 = 企业）';
-- ALTER TABLE `cert_table_extraction_result`
--   CHANGE COLUMN `EnterpriseCode` `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '企业 Code（本表归属维度 = 企业）';
--
-- -- 7.2 _config：加回 OwnerOrgCode（值 = 现 OrgCode），OrgCode 复位 yzh-std-env，索引复位
-- ALTER TABLE `cert_standard_directory_config`
--   ADD COLUMN `OwnerOrgCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '具体机构 Code（= sys_organization.Code / cert_certification_body.Code）；空 = 不特指机构（平台通用模板）' AFTER `OrgCode`;
-- UPDATE `cert_standard_directory_config` SET `OwnerOrgCode` = `OrgCode`;
-- UPDATE `cert_standard_directory_config` SET `OrgCode` = 'yzh-std-env';
-- ALTER TABLE `cert_standard_directory_config` DROP INDEX `uk_org_ent_std_stage`;
-- ALTER TABLE `cert_standard_directory_config`
--   ADD UNIQUE KEY `uk_owner_std_stage` (`OwnerOrgCode`, `EnterpriseCode`, `StandardCode`, `StageCode`);
--
-- -- 7.3 数据复位：仅还原快照中记录的行（11 表）
-- UPDATE `cert_iso_clause`      SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_iso_clause');
-- UPDATE `cert_fill_param_def`  SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_fill_param_def');
-- UPDATE `cert_report_section`  SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_report_section');
-- UPDATE `cert_validation_rule` SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_validation_rule');
-- UPDATE `cert_doc_fill_prompt` SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_doc_fill_prompt');
-- UPDATE `cert_iso_standard`    SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_iso_standard');
-- UPDATE `cert_ai_config`       SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_ai_config');
-- UPDATE `cert_conclusion_rule` SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_conclusion_rule');
-- UPDATE `cert_nc_conclude_prompt` SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_nc_conclude_prompt');
-- UPDATE `cert_nc_judge_prompt` SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='cert_nc_judge_prompt');
-- UPDATE `wf_prompt_template`   SET `OrgCode`='yzh-std-env' WHERE `Id` IN (SELECT `Id` FROM `_bak_20261013_owner_v2` WHERE `SrcTable`='wf_prompt_template');
--
-- -- 7.4 注释复位（如需精确复位，见 20261013_std_dir_config_owner_org_V1.sql §5）
-- -- 7.5 快照表保留（勿删，作为回退凭据）：_bak_20261013_owner_v2

-- ============================================================================
-- §8 可选：sys_organization 的 yzh-std-env 虚拟机构行
-- ----------------------------------------------------------------------------
-- 核查结论（2026-10-13）：后端零代码依赖（仅 CoreOrgCodes.cs 注释提及 3 处）；
--                       全库无外键引用 sys_organization。
-- ⇒ 新模型下该行不再需要，但删除无业务收益、且可能影响机构树显示。
-- ⇒ 本次【保留】，仅标记为废弃。如需删除，取消下面 2 行注释：
-- UPDATE `sys_organization` SET `Remark` = CONCAT(IFNULL(`Remark`,''), ' [2026-10-13 归属模型V2：已废弃，平台标准级改用空 OrgCode]') WHERE `Code` = 'yzh-std-env';
-- -- DELETE FROM `sys_organization` WHERE `Code` = 'yzh-std-env';
-- ============================================================================
