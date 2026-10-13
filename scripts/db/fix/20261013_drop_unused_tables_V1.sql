-- =====================================================================
-- 20261013_drop_unused_tables_V1.sql
-- 用途：清理「无效表 / 与项目无关的表」
-- 依据：docs/40-实施/分析报告/数据库规范符合性与无用表体检报告-V1.md
-- 备份：backup/db/yzh_cert_platform_before_cleanup_20261013_1004.sql（26MB，123 CREATE TABLE）
-- 裁决：用户 2026-10-13 指令「无效表，和项目无关的表全部清理掉」
--       ★ Sys_City / Sys_Province 明确保留
-- 安全前提：已核对 —— 无任何「保留表」的外键指向下列待删表
-- 幂等：IF EXISTS
-- =====================================================================

SET FOREIGN_KEY_CHECKS = 0;

-- ── A 组：备份 / 迁移临时 / 已裁决废弃（18 张） ──────────────────────
DROP TABLE IF EXISTS `_bak_20261008_cert_validation_rule`;
DROP TABLE IF EXISTS `_bak_20261012_std_dir_config`;
DROP TABLE IF EXISTS `_bak_20261012_std_dir_file`;
DROP TABLE IF EXISTS `_bak_20261012_std_dir_file_v2`;
DROP TABLE IF EXISTS `_bak_20261012_stdenv_ai_config`;
DROP TABLE IF EXISTS `_bak_20261012_stdenv_directory_config`;
DROP TABLE IF EXISTS `_bak_20261012_stdenv_prompt_template`;
DROP TABLE IF EXISTS `_bak_20261012_stdenv_report_section`;
DROP TABLE IF EXISTS `_bak_20261012_stdenv_validation_rule`;
DROP TABLE IF EXISTS `_bak_createby_20261011`;
DROP TABLE IF EXISTS `_bak_usercode_20261011`;
DROP TABLE IF EXISTS `_mig_usercode_20261011_result`;
DROP TABLE IF EXISTS `cert_validation_rule_bak_20261011`;
DROP TABLE IF EXISTS `sys_api_bak_20261003`;
DROP TABLE IF EXISTS `wf_skill_bak_20261011`;
DROP TABLE IF EXISTS `wf_skill_reflection_bak_20261011`;
DROP TABLE IF EXISTS `z_deprecated_cert_report_template`;
DROP TABLE IF EXISTS `z_deprecated_rpt_report_section_source`;

-- ── B 组：旧 Vol 遗留 / 零代码引用（19 张） ──────────────────────────
-- audit_* 家族：旧 Vol「审核项目/任务/检查表/证据/发现/NC/整改」
--   ⛔ 现场检查、多级审核、整改已明确不做；NC 已由 cert_expert_nc_* 承接
DROP TABLE IF EXISTS `audit_checklist_item`;
DROP TABLE IF EXISTS `audit_evidence`;
DROP TABLE IF EXISTS `audit_finding`;
DROP TABLE IF EXISTS `audit_nonconformity`;
DROP TABLE IF EXISTS `audit_project`;
DROP TABLE IF EXISTS `audit_rectification`;
DROP TABLE IF EXISTS `audit_task`;
-- 旧「认证申请」流程（申请/受理不做）
DROP TABLE IF EXISTS `cert_application`;
-- 审核员档案：已被框架 Sys_User 取代
DROP TABLE IF EXISTS `cert_auditor_profile`;
-- 条款提取规则：已被 cert_doc_extraction_rule + cert_validation_rule.RuleJson 取代
DROP TABLE IF EXISTS `cert_clause_extraction_rule`;
-- 校验规则溯源：字段级追溯归后台工作流（D08）
DROP TABLE IF EXISTS `cert_validation_rule_source`;
-- 旧报告任务/报告正文：已被 cert_expert_task / cert_expert_report_result 取代
DROP TABLE IF EXISTS `rpt_report_task`;
DROP TABLE IF EXISTS `rpt_audit_report`;
-- 字段标签映射 / api 型 Skill（预留未用）/ 工作流执行日志（规则 DAG 实存 RuleJson）
DROP TABLE IF EXISTS `wf_field_label_mapping`;
DROP TABLE IF EXISTS `wf_skill_api`;
DROP TABLE IF EXISTS `wf_workflow_execution_log`;
DROP TABLE IF EXISTS `wf_workflow_definition`;
-- 旧 Vol 接口权限机制（已被 sys_role_api + sys_user_permission 取代，且已清空）
DROP TABLE IF EXISTS `Sys_RoleAuth`;
-- 框架层空壳（SysConfig 实体实际映射 cert_sys_config）
DROP TABLE IF EXISTS `sys_config`;

-- ── 孤儿视图（2 个） ────────────────────────────────────────────────
-- v_workflow：仅依赖 wf_workflow_definition；代码中仅 1 处注释提及
-- v_certification_body：零代码引用
DROP VIEW IF EXISTS `v_workflow`;
DROP VIEW IF EXISTS `v_certification_body`;

SET FOREIGN_KEY_CHECKS = 1;

-- =====================================================================
-- 验证：期望 base_table = 86（123 - 37），views = 6（8 - 2）
-- =====================================================================
SELECT 'base_table' AS kind, COUNT(*) AS cnt FROM information_schema.TABLES
  WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_TYPE='BASE TABLE'
UNION ALL
SELECT 'views', COUNT(*) FROM information_schema.VIEWS WHERE TABLE_SCHEMA='yzh_cert_platform';
