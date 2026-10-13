-- ════════════════════════════════════════════════════════════════════════════════
--  2026-10-12  彻底解决 yzh-std-env 归属（V1）
--               ① 后台标准级 19 行 OrgCode 统一 → 'yzh-std-env'
--               ② 撤销 cert_standard_directory_file.OrgCode 冗余列（含索引）
--
--  ★ 终裁依据：docs/30-项目规则/跨端共享与核心资源检索规则-V1.md §2.0 / §2.8(f)
--    OrgCode 两值模型：'yzh-std-env'（统称机构 = 所有机构的统一含义 = 后台规则）
--    或 <企业 Code>（企业自有）；需要具体机构时新增列，⛔ 不复用 OrgCode。
--
--  ① 待迁 19 行（2026-10-12 裁决清单）：
--     cert_report_section            8 行（当前 = 机构 906e8b2a…）
--     cert_validation_rule           8 行（当前 = 机构 906e8b2a…）
--     cert_standard_directory_config 1 行（模板行 EnterpriseCode=''，当前 = 机构）
--     cert_ai_config                 1 行（当前 = NULL）
--     wf_prompt_template             1 行（当前 = NULL）
--
--  ② 撤列取证（业务使用 = 6 条证据链全阴性，用户裁决「涉及不多 → 一并删除」）：
--     · 实体类 StandardDirectoryFile.cs **无 OrgCode 属性** ⇒ C# 实体化读写物理不可达
--     · 全仓裸 SQL `WHERE OrgCode` 仅 1 处（cert_expert_task 任务编号），非本表
--     · 3 个引用本表的数据库视图无一 SELECT/WHERE 该列
--     · 前端 Field:'OrgCode' 过滤仅 4 处，全部打 report_section/validation_rule
--     · 「按机构取文件目录」正规路径 = ConfigCode → config.OrgCode 中转（6+ 处）
--     · 该列唯一使用者 = 昨日迁移脚本自身 + 其所建 idx_org_std_stage 索引
--
--  ★ 取代关系（V1 → V2 → 本脚本，file.OrgCode 列的三次裁决）：
--     20261012_unify_owner_field_V1.sql   23 行回填 yzh-std-env（误）
--     20261012_fix_std_dir_owner_V2.sql   改回机构 Code（其「三重证据」被终裁取代）
--     本脚本                             彻底撤列 —— 回到原设计
--       （实体注释：「子表 folder/file 不加机构列 —— 经 ConfigCode → config.OrgCode 间接归属」）
--     终裁：文件管理特殊 / MinIO 物理结构不动 ⇒ 存量 23 行 StoragePath 一律不改。
--
--  ★ 幂等：UPDATE 按「OrgCode IS NULL OR <> 'yzh-std-env'」定位；DDL 用
--     information_schema + PREPARE 条件执行，重复执行无副作用。
--  ★ 回滚：见 §5（快照表 _bak_20261012_stdenv_* 由 §1 建立）。
-- ════════════════════════════════════════════════════════════════════════════════

-- ── §1 修正前快照（5 表） ────────────────────────────────────────────────────────
DROP TABLE IF EXISTS `_bak_20261012_stdenv_report_section`;
CREATE TABLE `_bak_20261012_stdenv_report_section` AS
SELECT Id, Code, OrgCode FROM `cert_report_section`;

DROP TABLE IF EXISTS `_bak_20261012_stdenv_validation_rule`;
CREATE TABLE `_bak_20261012_stdenv_validation_rule` AS
SELECT Id, Code, OrgCode FROM `cert_validation_rule`;

DROP TABLE IF EXISTS `_bak_20261012_stdenv_directory_config`;
CREATE TABLE `_bak_20261012_stdenv_directory_config` AS
SELECT Id, Code, OrgCode, EnterpriseCode FROM `cert_standard_directory_config`;

DROP TABLE IF EXISTS `_bak_20261012_stdenv_ai_config`;
CREATE TABLE `_bak_20261012_stdenv_ai_config` AS
SELECT Id, Code, OrgCode FROM `cert_ai_config`;

DROP TABLE IF EXISTS `_bak_20261012_stdenv_prompt_template`;
CREATE TABLE `_bak_20261012_stdenv_prompt_template` AS
SELECT Id, Code, PromptCode, OrgCode FROM `wf_prompt_template`;

-- ── §2 19 行统一归属 yzh-std-env ────────────────────────────────────────────────
-- 2.1 cert_report_section（期望影响 8 行）
UPDATE `cert_report_section`
   SET OrgCode = 'yzh-std-env'
 WHERE OrgCode IS NULL OR OrgCode <> 'yzh-std-env';
SELECT ROW_COUNT() AS `2.1_report_section_影响行数_期望8`;

-- 2.2 cert_validation_rule（期望影响 8 行）
UPDATE `cert_validation_rule`
   SET OrgCode = 'yzh-std-env'
 WHERE OrgCode IS NULL OR OrgCode <> 'yzh-std-env';
SELECT ROW_COUNT() AS `2.2_validation_rule_影响行数_期望8`;

-- 2.3 cert_standard_directory_config —— 只动模板行（EnterpriseCode 为空），
--     企业行（EnterpriseCode = 企业 Code）不在此列终裁范围（当前 0 行）。（期望影响 1 行）
UPDATE `cert_standard_directory_config`
   SET OrgCode = 'yzh-std-env'
 WHERE (EnterpriseCode IS NULL OR EnterpriseCode = '')
   AND (OrgCode IS NULL OR OrgCode <> 'yzh-std-env');
SELECT ROW_COUNT() AS `2.3_directory_config_影响行数_期望1`;

-- 2.4 cert_ai_config（期望影响 1 行，当前 NULL）
UPDATE `cert_ai_config`
   SET OrgCode = 'yzh-std-env'
 WHERE OrgCode IS NULL OR OrgCode <> 'yzh-std-env';
SELECT ROW_COUNT() AS `2.4_ai_config_影响行数_期望1`;

-- 2.5 wf_prompt_template（期望影响 1 行，当前 NULL）
UPDATE `wf_prompt_template`
   SET OrgCode = 'yzh-std-env'
 WHERE OrgCode IS NULL OR OrgCode <> 'yzh-std-env';
SELECT ROW_COUNT() AS `2.5_prompt_template_影响行数_期望1`;

-- ── §3 撤销 file.OrgCode 列（幂等 DDL） ─────────────────────────────────────────
-- 3.1 删除索引 idx_org_std_stage（列不存在则索引已随列消失，条件跳过）
SET @has_idx := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_standard_directory_file'
    AND INDEX_NAME = 'idx_org_std_stage'
);
SET @ddl := IF(@has_idx > 0,
  'ALTER TABLE `cert_standard_directory_file` DROP INDEX `idx_org_std_stage`',
  'SELECT ''3.1 索引已不存在，跳过'' AS Info');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 3.2 删除列 OrgCode（不存在则跳过）
SET @has_col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_standard_directory_file'
    AND COLUMN_NAME = 'OrgCode'
);
SET @ddl := IF(@has_col > 0,
  'ALTER TABLE `cert_standard_directory_file` DROP COLUMN `OrgCode`',
  'SELECT ''3.2 列已不存在，跳过'' AS Info');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ── §4 验证 ─────────────────────────────────────────────────────────────────────
-- 4.1 五表归属分布（期望：每表全部 = yzh-std-env，合计 19 行）
SELECT '--- 验证 4.1 五表 OrgCode 分布（期望全 = yzh-std-env）---' AS Info;
SELECT 'cert_report_section' AS tbl, IFNULL(OrgCode, '<NULL>') AS OrgCode, COUNT(*) AS cnt
  FROM `cert_report_section` GROUP BY OrgCode
UNION ALL
SELECT 'cert_validation_rule', IFNULL(OrgCode, '<NULL>'), COUNT(*)
  FROM `cert_validation_rule` GROUP BY OrgCode
UNION ALL
SELECT 'cert_standard_directory_config', IFNULL(OrgCode, '<NULL>'), COUNT(*)
  FROM `cert_standard_directory_config` GROUP BY OrgCode
UNION ALL
SELECT 'cert_ai_config', IFNULL(OrgCode, '<NULL>'), COUNT(*)
  FROM `cert_ai_config` GROUP BY OrgCode
UNION ALL
SELECT 'wf_prompt_template', IFNULL(OrgCode, '<NULL>'), COUNT(*)
  FROM `wf_prompt_template` GROUP BY OrgCode;

-- 4.2 file 表列 / 索引必须已消失（期望 0 / 0）
SELECT '--- 验证 4.2 file.OrgCode 列与索引已删除（期望 0 / 0）---' AS Info;
SELECT (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = 'cert_standard_directory_file'
           AND COLUMN_NAME = 'OrgCode')                          AS `C_列存在_期望0`,
       (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = 'cert_standard_directory_file'
           AND INDEX_NAME = 'idx_org_std_stage')                 AS `C_索引存在_期望0`;

-- 4.3 综合违规计数（期望 C_违规 = 0）
SELECT '--- 验证 4.3 综合违规（期望 0）---' AS Info;
SELECT (SELECT COUNT(*) FROM `cert_report_section`
         WHERE IFNULL(OrgCode, '') <> 'yzh-std-env')
     + (SELECT COUNT(*) FROM `cert_validation_rule`
         WHERE IFNULL(OrgCode, '') <> 'yzh-std-env')
     + (SELECT COUNT(*) FROM `cert_standard_directory_config`
         WHERE (EnterpriseCode IS NULL OR EnterpriseCode = '')
           AND IFNULL(OrgCode, '') <> 'yzh-std-env')
     + (SELECT COUNT(*) FROM `cert_ai_config`
         WHERE IFNULL(OrgCode, '') <> 'yzh-std-env')
     + (SELECT COUNT(*) FROM `wf_prompt_template`
         WHERE IFNULL(OrgCode, '') <> 'yzh-std-env')
     AS C_违规;

-- ── §5 回滚（默认注释，需要时手工执行） ─────────────────────────────────────────
-- -- 5.1 恢复五表旧值
-- UPDATE `cert_report_section` t
--   JOIN `_bak_20261012_stdenv_report_section` b ON b.Id = t.Id
--    SET t.OrgCode = b.OrgCode;
-- UPDATE `cert_validation_rule` t
--   JOIN `_bak_20261012_stdenv_validation_rule` b ON b.Id = t.Id
--    SET t.OrgCode = b.OrgCode;
-- UPDATE `cert_standard_directory_config` t
--   JOIN `_bak_20261012_stdenv_directory_config` b ON b.Id = t.Id
--    SET t.OrgCode = b.OrgCode;
-- UPDATE `cert_ai_config` t
--   JOIN `_bak_20261012_stdenv_ai_config` b ON b.Id = t.Id
--    SET t.OrgCode = b.OrgCode;
-- UPDATE `wf_prompt_template` t
--   JOIN `_bak_20261012_stdenv_prompt_template` b ON b.Id = t.Id
--    SET t.OrgCode = b.OrgCode;
-- -- 5.2 重建 file.OrgCode 列与索引（列 varchar(50) NULL DEFAULT NULL，与撤列前一致）
-- ALTER TABLE `cert_standard_directory_file`
--   ADD COLUMN `OrgCode` VARCHAR(50) NULL DEFAULT NULL COMMENT '冗余归属列（2026-10-12 撤销；回滚用）' AFTER `FolderCode`;
-- ALTER TABLE `cert_standard_directory_file`
--   ADD INDEX `idx_org_std_stage` (`OrgCode`, `StandardCode`, `StageCode`);
