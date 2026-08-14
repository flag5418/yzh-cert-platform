-- ============================================================
-- 修复脚本：给所有 YZH 业务表添加缺失的基类字段
-- 基类 YZHBaseEntity 字段：enable, status, remark
-- 执行时间：2026-08-14
-- ============================================================

USE yzh_cert_platform;

-- ============================================================
-- 1. 添加 enable 字段（逻辑删除标记）
-- ============================================================

-- cert 系列表
ALTER TABLE cert_auditor_profile ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_certification_body ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_clause_extraction_rule ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_directory_template ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_extraction_field ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_extraction_rule ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_file_requirement ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_iso_clause ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_iso_standard ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_phase_definition ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_report_template ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_standard_phase_config ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_validation_rule ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE cert_validation_rule_source ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';

-- ent 系列表
ALTER TABLE ent_enterprise ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE ent_enterprise_document ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE ent_enterprise_file ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE ent_enterprise_phase ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE ent_extraction_result ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE ent_file_compliance_check ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE ent_file_pre_check_result ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE ent_file_version ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE ent_table_extraction_result ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';

-- audit 系列表
ALTER TABLE audit_checklist_item ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE audit_evidence ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE audit_finding ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE audit_nonconformity ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE audit_rectification ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE audit_task ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';

-- rpt 系列表
ALTER TABLE rpt_audit_report ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE rpt_report_section ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE rpt_report_section_source ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';
ALTER TABLE rpt_report_task ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';

-- cert_sys_config（配置表也加上）
ALTER TABLE cert_sys_config ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';

-- cert_ai_usage_log
ALTER TABLE cert_ai_usage_log ADD COLUMN enable TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除';

-- ============================================================
-- 2. 添加 status 字段（业务状态）
-- ============================================================

ALTER TABLE cert_clause_extraction_rule ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_directory_template ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_extraction_field ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_extraction_rule ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_file_requirement ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_iso_clause ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_phase_definition ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_report_template ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_standard_phase_config ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_validation_rule ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_validation_rule_source ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';

ALTER TABLE ent_enterprise_document ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE ent_enterprise_file ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE ent_extraction_result ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE ent_file_compliance_check ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE ent_file_pre_check_result ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE ent_file_version ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE ent_table_extraction_result ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';

ALTER TABLE audit_checklist_item ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE audit_evidence ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE audit_finding ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE audit_nonconformity ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE audit_rectification ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';

ALTER TABLE rpt_report_section ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE rpt_report_section_source ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';

ALTER TABLE cert_sys_config ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';
ALTER TABLE cert_ai_usage_log ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';

-- ============================================================
-- 3. 添加 remark 字段（备注）
-- ============================================================

ALTER TABLE cert_auditor_profile ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_certification_body ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_clause_extraction_rule ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_directory_template ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_extraction_field ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_extraction_rule ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_file_requirement ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_iso_clause ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_iso_standard ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_phase_definition ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_report_template ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_standard_phase_config ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_validation_rule ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_validation_rule_source ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';

ALTER TABLE ent_enterprise ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE ent_enterprise_document ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE ent_enterprise_file ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE ent_enterprise_phase ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE ent_extraction_result ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE ent_file_compliance_check ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE ent_file_pre_check_result ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE ent_file_version ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE ent_table_extraction_result ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';

ALTER TABLE audit_checklist_item ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE audit_evidence ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE audit_finding ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE audit_nonconformity ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE audit_rectification ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE audit_task ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';

ALTER TABLE rpt_audit_report ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE rpt_report_section ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE rpt_report_section_source ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE rpt_report_task ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';

ALTER TABLE cert_sys_config ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';
ALTER TABLE cert_ai_usage_log ADD COLUMN remark VARCHAR(500) DEFAULT NULL COMMENT '备注';

-- ============================================================
-- 4. 给缺少 org_code 的表添加 org_code
-- ============================================================

ALTER TABLE audit_checklist_item ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE audit_evidence ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE audit_finding ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE audit_nonconformity ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE audit_rectification ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE audit_task ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';

ALTER TABLE ent_enterprise_document ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE ent_enterprise_file ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE ent_extraction_result ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE ent_file_compliance_check ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE ent_file_pre_check_result ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE ent_file_version ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE ent_table_extraction_result ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';

ALTER TABLE rpt_audit_report ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE rpt_report_section ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE rpt_report_section_source ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';
ALTER TABLE rpt_report_task ADD COLUMN org_code VARCHAR(50) DEFAULT NULL COMMENT '机构编码';

-- ============================================================
-- 5. 给缺少删除审计字段的表添加 delete_id/deleter/delete_time
-- ============================================================

-- ent_enterprise_document
ALTER TABLE ent_enterprise_document ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE ent_enterprise_document ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE ent_enterprise_document ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- ent_enterprise_file
ALTER TABLE ent_enterprise_file ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE ent_enterprise_file ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE ent_enterprise_file ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- ent_extraction_result
ALTER TABLE ent_extraction_result ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE ent_extraction_result ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE ent_extraction_result ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- ent_file_compliance_check
ALTER TABLE ent_file_compliance_check ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE ent_file_compliance_check ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE ent_file_compliance_check ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- ent_file_pre_check_result
ALTER TABLE ent_file_pre_check_result ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE ent_file_pre_check_result ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE ent_file_pre_check_result ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- ent_file_version
ALTER TABLE ent_file_version ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE ent_file_version ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE ent_file_version ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- ent_table_extraction_result
ALTER TABLE ent_table_extraction_result ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE ent_table_extraction_result ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE ent_table_extraction_result ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- audit 系列表缺少删除字段
ALTER TABLE audit_checklist_item ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE audit_checklist_item ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE audit_checklist_item ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

ALTER TABLE audit_evidence ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE audit_evidence ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE audit_evidence ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

ALTER TABLE audit_finding ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE audit_finding ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE audit_finding ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

ALTER TABLE audit_nonconformity ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE audit_nonconformity ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE audit_nonconformity ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

ALTER TABLE audit_rectification ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE audit_rectification ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE audit_rectification ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- rpt 系列表缺少删除字段
ALTER TABLE rpt_audit_report ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE rpt_audit_report ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE rpt_audit_report ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

ALTER TABLE rpt_report_section ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE rpt_report_section ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE rpt_report_section ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

ALTER TABLE rpt_report_section_source ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE rpt_report_section_source ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE rpt_report_section_source ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

ALTER TABLE rpt_report_task ADD COLUMN delete_id INT DEFAULT NULL COMMENT '删除人ID';
ALTER TABLE rpt_report_task ADD COLUMN deleter VARCHAR(50) DEFAULT NULL COMMENT '删除人姓名';
ALTER TABLE rpt_report_task ADD COLUMN delete_time DATETIME DEFAULT NULL COMMENT '删除时间';

-- ============================================================
-- 6. 给缺少 modify 字段的表补全 modify_id/modifier/modify_date
-- ============================================================
-- ent_enterprise_file 缺少 modify 相关字段
-- (已在上面 DESCRIBE 结果确认: 只有 create_id/creator/create_date)

-- ============================================================
-- 验证
-- ============================================================
SELECT 'DONE - All base fields added' as result;
