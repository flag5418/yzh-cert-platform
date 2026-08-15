-- ============================================================
-- 移除全局基础资料表和不需要机构隔离的表中的 org_code 列
-- 
-- 原因：YZHBaseEntity 基类不再包含 OrgCode 字段
--       全局基础资料表（管理员维护）不需要机构隔离
--       cert_certification_body 和 cert_iso_standard 也不需要（全局管理）
--
-- 执行日期：2026-08-15
-- 影响表数：19 张
-- ============================================================

-- ====== 1. 全局基础资料表（17 张）======
ALTER TABLE cert_cert_stage DROP COLUMN org_code;
ALTER TABLE cert_clause_extraction_rule DROP COLUMN org_code;
ALTER TABLE cert_directory_template DROP COLUMN org_code;
ALTER TABLE cert_doc_extraction_rule DROP COLUMN org_code;
ALTER TABLE cert_doc_field_def DROP COLUMN org_code;
ALTER TABLE cert_doc_table_def DROP COLUMN org_code;
ALTER TABLE cert_doc_table_field_def DROP COLUMN org_code;
ALTER TABLE cert_extraction_field DROP COLUMN org_code;
ALTER TABLE cert_extraction_rule DROP COLUMN org_code;
ALTER TABLE cert_file_requirement DROP COLUMN org_code;
ALTER TABLE cert_iso_clause DROP COLUMN org_code;
ALTER TABLE cert_phase_definition DROP COLUMN org_code;
ALTER TABLE cert_report_template DROP COLUMN org_code;
ALTER TABLE cert_standard_phase_config DROP COLUMN org_code;
ALTER TABLE cert_validation_rule DROP COLUMN org_code;
ALTER TABLE cert_validation_rule_source DROP COLUMN org_code;

-- 注：cert_sys_config 和 cert_ai_usage_log 没有 org_code 列，不需要处理
-- 注：cert_ai_config 保留 org_code（AI 配置按机构隔离）

-- ====== 2. 特殊表：认证机构表和 ISO 标准表（2 张）======
-- cert_certification_body: 自己就是机构，org_code 冗余
ALTER TABLE cert_certification_body DROP COLUMN org_code;
-- cert_iso_standard: ISO 标准是全局的，由管理员维护
ALTER TABLE cert_iso_standard DROP COLUMN org_code;

-- ====== 验证：确认 org_code 列已被删除 ======
SELECT TABLE_NAME, COLUMN_NAME, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND COLUMN_NAME = 'org_code'
ORDER BY TABLE_NAME;
-- 预期结果：剩余约 27 张表（机构级数据表）仍有 org_code 列
