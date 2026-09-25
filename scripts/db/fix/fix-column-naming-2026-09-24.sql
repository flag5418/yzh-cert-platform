-- ============================================================
-- YZH 命名规范 & Enable 清零修正脚本 V4
-- 铁律七：DB列名 = C#属性名 = TS字段名 = PascalCase 逐字一致
-- 铁律九：全库禁止 `Enable` 列；启禁语义统一走 IIsValid.IsValid（1=有效 0=无效）
-- 生成: 2026-09-24 | V4 更新: 2026-09-25（第 5 节全部启用，D-E1/D-E2 已裁决）
-- 来源: information_schema（精确列定义，非推断）
-- 验证: 已在临时库 yzh_fix_dryrun 全量 dry-run 通过（残留违规 0）
-- ------------------------------------------------------------
-- 变更前实测基线（2026-09-25，verify_naming.sql）：
--   正式表 100 张 / 视图 9 个 / 违规列 123 个（非PascalCase 87 + Enable 36）
--   ① 命名违规（非 PascalCase）：33 张表 / 87 列
--   ② 小写 `enable` 且已有 IsValid → DROP：25 张表
--   ③ 大写 `Enable` 且已有 IsValid → DROP：1 张表 ['sys_config']
--   ④ `Enable` 无 IsValid：9 张表
--      · 5A 改名：sys_api（代码已同步改造）
--      · 5B 删列：sys_log（孤儿列，实体未声明）
--      · 5C 整表 DROP（D-E1）：Sys_TableColumn / Sys_TableInfo / Sys_UserDepartment
--                              / Sys_WorkFlow / Sys_WorkFlowStep / Sys_WorkFlowTable / Sys_WorkFlowTableStep
-- ------------------------------------------------------------
-- ★ 执行前：① 全库备份（命令见文末）② 停后端 ③ 已实测违规表 0 行数据
-- ★ 执行后：跑文末「验证 SQL」，期望 0 行
-- ★★ 5A 依赖代码已同步（SysApi : IIsValid + ApiSyncService + RoleApiController
--     + SysApi.json + 前端 api 页/类型）—— 已 2026-09-25 完成，编译 0 error
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
SET FOREIGN_KEY_CHECKS = 0;

-- ═══════════════════════════════════════════════════════
-- 第 1 节：DROP 备份表（12 张，非正式结构）
-- ═══════════════════════════════════════════════════════
DROP TABLE IF EXISTS `_bak_20260924_cert_standard_directory_config`;
DROP TABLE IF EXISTS `_bak_20260924_cert_standard_directory_file`;
DROP TABLE IF EXISTS `_bak_20260924_cert_standard_directory_folder`;
DROP TABLE IF EXISTS `_bak_20260924_rpt_report_section`;
DROP TABLE IF EXISTS `_bak_20260924_sys_dictionary`;
DROP TABLE IF EXISTS `_bak_20260924_sys_dictionarylist`;
DROP TABLE IF EXISTS `_bak_20260924_sys_menu`;
DROP TABLE IF EXISTS `_bak_20260924_sys_organization`;
DROP TABLE IF EXISTS `_bak_20260924_sys_role`;
DROP TABLE IF EXISTS `_bak_20260924_sys_user`;
DROP TABLE IF EXISTS `_bak_20260924_wf_prompt_template`;
DROP TABLE IF EXISTS `_bak_20260924_wf_workflow_definition`;

-- ═══════════════════════════════════════════════════════
-- 第 2 节：命名修正 — 非 PascalCase 列改名（33 张表 / 86 列）
--   列定义逐列取自 information_schema（类型/可空/默认值/注释/AUTO_INCREMENT 原样保留）
-- ═══════════════════════════════════════════════════════

-- audit_checklist_item
ALTER TABLE `audit_checklist_item`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- audit_evidence
ALTER TABLE `audit_evidence`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- audit_finding
ALTER TABLE `audit_finding`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- audit_nonconformity
ALTER TABLE `audit_nonconformity`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- audit_project
ALTER TABLE `audit_project`
  CHANGE COLUMN `status` `Status` varchar(20) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- audit_rectification
ALTER TABLE `audit_rectification`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- cert_ai_usage_log
ALTER TABLE `cert_ai_usage_log`
  CHANGE COLUMN `success` `Success` tinyint(1) NULL DEFAULT 1 COMMENT '是否成功'  -- 铁律七：success → Success
;

-- ent_enterprise_document
ALTER TABLE `ent_enterprise_document`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- ent_enterprise_file
ALTER TABLE `ent_enterprise_file`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- ent_enterprise_phase
ALTER TABLE `ent_enterprise_phase`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- ent_extraction_result
ALTER TABLE `ent_extraction_result`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- ent_file_compliance_check
ALTER TABLE `ent_file_compliance_check`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- ent_file_pre_check_result
ALTER TABLE `ent_file_pre_check_result`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- ent_file_version
ALTER TABLE `ent_file_version`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- ent_table_extraction_result
ALTER TABLE `ent_table_extraction_result`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- rpt_audit_report
ALTER TABLE `rpt_audit_report`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- rpt_report_section
ALTER TABLE `rpt_report_section`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- rpt_report_section_source
ALTER TABLE `rpt_report_section_source`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- rpt_report_task
ALTER TABLE `rpt_report_task`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- wf_execution_task
ALTER TABLE `wf_execution_task`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',  -- 铁律七：code → Code
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT 'active' COMMENT '业务状态',  -- 铁律七：status → Status
  CHANGE COLUMN `sort` `Sort` int NULL DEFAULT 0 COMMENT '排序号',  -- 铁律七：sort → Sort
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL COMMENT '备注'  -- 铁律七：remark → Remark
;

-- wf_execution_task_item
ALTER TABLE `wf_execution_task_item`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',  -- 铁律七：code → Code
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT 'active' COMMENT '业务状态',  -- 铁律七：status → Status
  CHANGE COLUMN `sort` `Sort` int NULL DEFAULT 0 COMMENT '排序号',  -- 铁律七：sort → Sort
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL COMMENT '备注'  -- 铁律七：remark → Remark
;

-- wf_field_label_mapping
ALTER TABLE `wf_field_label_mapping`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- wf_node_execution
ALTER TABLE `wf_node_execution`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',  -- 铁律七：code → Code
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT 'active' COMMENT '业务状态',  -- 铁律七：status → Status
  CHANGE COLUMN `sort` `Sort` int NULL DEFAULT 0 COMMENT '排序号',  -- 铁律七：sort → Sort
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL COMMENT '备注'  -- 铁律七：remark → Remark
;

-- wf_prompt_template
ALTER TABLE `wf_prompt_template`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键',  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(100) NOT NULL COMMENT '全局唯一编码（GUID）',  -- 铁律七：code → Code
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT 'active' COMMENT '实体启用状态',  -- 铁律七：status → Status
  CHANGE COLUMN `sort` `Sort` int NULL DEFAULT 0 COMMENT '排序',  -- 铁律七：sort → Sort
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL COMMENT '备注',  -- 铁律七：remark → Remark
  CHANGE COLUMN `template` `Template` mediumtext NULL DEFAULT NULL COMMENT '提示词模板（支持占位符）',  -- 铁律七：template → Template
  CHANGE COLUMN `description` `Description` text NULL DEFAULT NULL COMMENT '说明',  -- 铁律七：description → Description
  CHANGE COLUMN `version` `Version` int NOT NULL DEFAULT 1 COMMENT '版本号'  -- 铁律七：version → Version
;

-- wf_skill
ALTER TABLE `wf_skill`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT,  -- 铁律七：id → Id
  CHANGE COLUMN `name` `Name` varchar(200) NOT NULL COMMENT 'Skill名称',  -- 铁律七：name → Name
  CHANGE COLUMN `description` `Description` text NULL DEFAULT NULL COMMENT '说明',  -- 铁律七：description → Description
  CHANGE COLUMN `version` `Version` varchar(20) NULL DEFAULT '1.0' COMMENT '版本',  -- 铁律七：version → Version
  CHANGE COLUMN `icon` `Icon` varchar(50) NULL DEFAULT NULL COMMENT '图标',  -- 铁律七：icon → Icon
  CHANGE COLUMN `color` `Color` varchar(20) NULL DEFAULT NULL COMMENT '颜色',  -- 铁律七：color → Color
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL  -- 铁律七：remark → Remark
;

-- wf_skill_api
ALTER TABLE `wf_skill_api`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT,  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(100) NOT NULL,  -- 铁律七：code → Code
  CHANGE COLUMN `url` `Url` varchar(500) NOT NULL,  -- 铁律七：url → Url
  CHANGE COLUMN `headers` `Headers` text NULL DEFAULT NULL COMMENT '请求头 JSON（值可含 $sys. 引用）',  -- 铁律七：headers → Headers
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT 'active',  -- 铁律七：status → Status
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL  -- 铁律七：remark → Remark
;

-- wf_skill_category
ALTER TABLE `wf_skill_category`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT,  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(100) NOT NULL,  -- 铁律七：code → Code
  CHANGE COLUMN `icon` `Icon` varchar(50) NULL DEFAULT NULL COMMENT '图标',  -- 铁律七：icon → Icon
  CHANGE COLUMN `color` `Color` varchar(20) NULL DEFAULT NULL COMMENT '颜色',  -- 铁律七：color → Color
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL  -- 铁律七：remark → Remark
;

-- wf_skill_input
ALTER TABLE `wf_skill_input`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT,  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(100) NOT NULL,  -- 铁律七：code → Code
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT 'active',  -- 铁律七：status → Status
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL  -- 铁律七：remark → Remark
;

-- wf_skill_output
ALTER TABLE `wf_skill_output`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT,  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(100) NOT NULL,  -- 铁律七：code → Code
  CHANGE COLUMN `description` `Description` varchar(500) NULL DEFAULT NULL,  -- 铁律七：description → Description
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT 'active',  -- 铁律七：status → Status
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL  -- 铁律七：remark → Remark
;

-- wf_skill_reflection
ALTER TABLE `wf_skill_reflection`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT,  -- 铁律七：id → Id
  CHANGE COLUMN `code` `Code` varchar(100) NOT NULL,  -- 铁律七：code → Code
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT 'active',  -- 铁律七：status → Status
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT NULL  -- 铁律七：remark → Remark
;

-- wf_workflow_execution_log
ALTER TABLE `wf_workflow_execution_log`
  CHANGE COLUMN `status` `Status` varchar(50) NULL DEFAULT NULL  -- 铁律七：status → Status
;

-- yzh_field_config
ALTER TABLE `yzh_field_config`
  CHANGE COLUMN `sortable` `Sortable` tinyint NULL DEFAULT 1,  -- 铁律七：sortable → Sortable
  CHANGE COLUMN `align` `Align` varchar(10) NULL DEFAULT 'left',  -- 铁律七：align → Align
  CHANGE COLUMN `required` `Required` tinyint NULL DEFAULT 0,  -- 铁律七：required → Required
  CHANGE COLUMN `maxlength` `Maxlength` int NULL DEFAULT 0,  -- 铁律七：maxlength → Maxlength
  CHANGE COLUMN `placeholder` `Placeholder` varchar(200) NULL DEFAULT '',  -- 铁律七：placeholder → Placeholder
  CHANGE COLUMN `readonly` `Readonly` tinyint NULL DEFAULT 0,  -- 铁律七：readonly → Readonly
  CHANGE COLUMN `disabled` `Disabled` tinyint NULL DEFAULT 0,  -- 铁律七：disabled → Disabled
  CHANGE COLUMN `precision` `Precision` int NULL DEFAULT NULL,  -- 铁律七：precision → Precision
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT ''  -- 铁律七：remark → Remark
;

-- yzh_page_config
ALTER TABLE `yzh_page_config`
  CHANGE COLUMN `stripe` `Stripe` tinyint NULL DEFAULT 1,  -- 铁律七：stripe → Stripe
  CHANGE COLUMN `remark` `Remark` varchar(500) NULL DEFAULT ''  -- 铁律七：remark → Remark
;

-- ═══════════════════════════════════════════════════════
-- 第 3 节：`enable` → DROP（25 张表，表内已有 IsValid）
--   铁律九：IIsValid 是唯一启禁契约；`enable` 与之并存属语义冲突，直接删除
-- ═══════════════════════════════════════════════════════
ALTER TABLE `audit_checklist_item` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `audit_evidence` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `audit_finding` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `audit_nonconformity` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `audit_rectification` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `ent_enterprise_document` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `ent_enterprise_file` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `ent_enterprise_phase` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `ent_extraction_result` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `ent_file_compliance_check` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `ent_file_pre_check_result` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `ent_file_version` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `ent_table_extraction_result` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `rpt_audit_report` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `rpt_report_section_source` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `rpt_report_task` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_execution_task` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_execution_task_item` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_field_label_mapping` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_node_execution` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_skill_api` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_skill_input` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_skill_output` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_skill_reflection` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存
ALTER TABLE `wf_workflow_execution_log` DROP COLUMN `enable`;  -- 铁律九：与 IsValid 并存

-- ═══════════════════════════════════════════════════════
-- 第 4 节：`Enable` → DROP（1 张表，表内已有 IsValid）
-- ═══════════════════════════════════════════════════════
ALTER TABLE `sys_config` DROP COLUMN `Enable`;  -- 铁律九：与 IsValid 并存

-- ═══════════════════════════════════════════════════════
-- 第 5 节：需代码同步的表（★ 2026-09-25 用户裁决后全部启用）
--   5A sys_api：ApiSync 同步源。实体 SysApi（SysPermission.cs）已改为实现 IIsValid
--      同步改：SysApi : IIsValid（bool Enable → int IsValid）+ ApiSyncService + RoleApiController.cs
--              + SysApi.json（EnableField 值 + 列定义）+ 前端 pages/system/api/{index.vue,logic.ts}
--              + 前端 api/system/api.ts（ApiItem.Enable → IsValid）
--   5B sys_log：DB 有 Enable 但实体 SysLog 未声明 → 孤儿列，直接 DROP
--   5C Vol 遗留表 → 决策 D-E1 = **整表 DROP**（用户明确：Vol 架构表不再使用）
--      ⚠️ 已核实：7 表全部 0 行 / src 零引用 / 无外键指向 / 无视图引用
-- ═══════════════════════════════════════════════════════

-- 5B：sys_log.Enable 是孤儿列（实体 SysLog 未声明）
ALTER TABLE `sys_log` DROP COLUMN `Enable`;  -- 铁律九：孤儿列，实体未声明

-- 5A：sys_api.Enable → IsValid（代码已同步改造完成）
ALTER TABLE `sys_api`
  CHANGE COLUMN `Enable` `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT '有效标志(1=有效,0=无效)';

-- 5C：7 张 Vol 遗留表整表 DROP（决策 D-E1 = ①整表 DROP）
--   依据：用户明确「我们的概念和vol理念完全不同，这些vol架构的表我明确不会使用了」
--   安全证据：0 行数据 + src/ 零引用 + 无外键 + 无视图 + 无 EntityConfig
DROP TABLE IF EXISTS `Sys_TableColumn`;
DROP TABLE IF EXISTS `Sys_TableInfo`;
DROP TABLE IF EXISTS `Sys_UserDepartment`;
DROP TABLE IF EXISTS `Sys_WorkFlow`;
DROP TABLE IF EXISTS `Sys_WorkFlowStep`;
DROP TABLE IF EXISTS `Sys_WorkFlowTable`;
DROP TABLE IF EXISTS `Sys_WorkFlowTableStep`;

SET FOREIGN_KEY_CHECKS = 1;

-- ═══════════════════════════════════════════════════════
-- 验证 SQL（期望 0 行）
--
-- ⚠️⚠️ 必须用 CONVERT(... COLLATE utf8mb4_bin)！
--   information_schema.COLUMN_NAME 的排序规则是大小写不敏感（_ci），
--   直接写 `COLUMN_NAME NOT REGEXP '^[A-Z]...'` 会让小写列也「匹配通过」
--   → **永远返回 0 行**（假阴性），让人误以为已清零。
--   （同理 `BINARY COLUMN_NAME` 会报 ERROR 3995，不可用于 REGEXP。）
-- ═══════════════════════════════════════════════════════
-- SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA='yzh_cert_platform'
--    AND TABLE_NAME NOT LIKE '\\_bak\\_%'
--    AND (CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin
--           NOT REGEXP '^[A-Z][A-Za-z0-9]*$'
--         OR BINARY COLUMN_NAME IN ('enable','Enable'));

-- ═══════════════════════════════════════════════════════
-- 备份命令（执行前跑）
-- ═══════════════════════════════════════════════════════
-- mkdir -p scripts/db/backup && docker exec yzh-mysql mysqldump -uroot -pYzh123456. \
--   --default-character-set=utf8mb4 --single-transaction --routines --triggers --events \
--   yzh_cert_platform > scripts/db/backup/yzh_cert_platform_before_naming_fix_$(date +%Y%m%d_%H%M).sql
