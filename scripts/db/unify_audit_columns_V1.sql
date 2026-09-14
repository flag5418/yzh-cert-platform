-- ============================================================
-- 审计字段列名统一脚本 V1
-- 目的：将所有 cert_* 表的审计字段统一为 BaseEntity PascalCase 命名
-- 日期：2026-09-12
-- 注意：执行前请备份数据库
-- 使用：mysql -uroot -p yzh_cert_platform < unify_audit_columns_V1.sql
-- ============================================================

SET FOREIGN_KEY_CHECKS = 0;

-- ============================================================
-- 第一步：DROP 受影响的视图（后续重建）
-- ============================================================
DROP VIEW IF EXISTS v_cert_phase_definition;
DROP VIEW IF EXISTS v_cert_stage;
DROP VIEW IF EXISTS v_certification_body;
DROP VIEW IF EXISTS v_iso_standard;
DROP VIEW IF EXISTS v_workflow;

-- ============================================================
-- 第二步~第三十步：各表审计字段统一改名
-- 规则：
--   create_date/create_time → CreateTime
--   creator/create_by/CreateBy → CreateBy（有重复列则删除 creator）
--   modifier/update_by/UpdateBy → UpdateBy（有重复列则删除 modifier）
--   deleter/delete_by/DeleteBy → DeleteBy（有重复列则删除 deleter）
--   modify_date/update_date/update_time → UpdateTime
--   delete_time → DeleteTime
--   is_deleted → IsDeleted
--   enable/is_valid → IsValid
--   sort/sort_order → Sort
--   remark → Remark
--   org_code → OrgCode
-- ============================================================

-- cert_ai_config：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_ai_config DROP COLUMN creator;
ALTER TABLE cert_ai_config DROP COLUMN modifier;
ALTER TABLE cert_ai_config DROP COLUMN deleter;
ALTER TABLE cert_ai_config DROP COLUMN update_date;
ALTER TABLE cert_ai_config RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_ai_config RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_ai_config RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_ai_config RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_ai_config RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_ai_config RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_ai_config RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_ai_config RENAME COLUMN sort TO Sort;
ALTER TABLE cert_ai_config RENAME COLUMN remark TO Remark;
ALTER TABLE cert_ai_config RENAME COLUMN org_code TO OrgCode;

-- cert_ai_usage_log：仅部分审计字段
ALTER TABLE cert_ai_usage_log RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_ai_usage_log RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_ai_usage_log RENAME COLUMN remark TO Remark;

-- cert_application：仅 create_by/update_by（无 creator/modifier）
ALTER TABLE cert_application RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_application RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_application RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_application RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_application RENAME COLUMN remark TO Remark;

-- cert_auditor_profile：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_auditor_profile DROP COLUMN creator;
ALTER TABLE cert_auditor_profile DROP COLUMN modifier;
ALTER TABLE cert_auditor_profile DROP COLUMN deleter;
ALTER TABLE cert_auditor_profile RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_auditor_profile RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_auditor_profile RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_auditor_profile RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_auditor_profile RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_auditor_profile RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_auditor_profile RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_auditor_profile RENAME COLUMN remark TO Remark;
ALTER TABLE cert_auditor_profile RENAME COLUMN org_code TO OrgCode;

-- cert_certification_body：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_certification_body DROP COLUMN creator;
ALTER TABLE cert_certification_body DROP COLUMN modifier;
ALTER TABLE cert_certification_body DROP COLUMN deleter;
ALTER TABLE cert_certification_body RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_certification_body RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_certification_body RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_certification_body RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_certification_body RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_certification_body RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_certification_body RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_certification_body RENAME COLUMN org_code TO OrgCode;

-- cert_clause_extraction_rule：只有 creator/modifier/deleter（无 create_by）
ALTER TABLE cert_clause_extraction_rule RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_clause_extraction_rule RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_clause_extraction_rule RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_clause_extraction_rule RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_clause_extraction_rule RENAME COLUMN org_code TO OrgCode;

-- cert_directory_template：只有 creator/modifier/deleter（无 create_by）
ALTER TABLE cert_directory_template RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_directory_template RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_directory_template RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_directory_template RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_directory_template RENAME COLUMN org_code TO OrgCode;

-- cert_doc_extraction_rule：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
-- 注意：此表有 is_valid（业务列），不与 enable 合并
ALTER TABLE cert_doc_extraction_rule DROP COLUMN creator;
ALTER TABLE cert_doc_extraction_rule DROP COLUMN modifier;
ALTER TABLE cert_doc_extraction_rule DROP COLUMN deleter;
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN enable TO IsValid;
-- is_valid（业务列，非审计）→ 保留原名，不改名

-- cert_doc_field_def：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_doc_field_def DROP COLUMN creator;
ALTER TABLE cert_doc_field_def DROP COLUMN modifier;
ALTER TABLE cert_doc_field_def DROP COLUMN deleter;
ALTER TABLE cert_doc_field_def RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_doc_field_def RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_doc_field_def RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_doc_field_def RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_doc_field_def RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_doc_field_def RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_doc_field_def RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_doc_field_def RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_field_def RENAME COLUMN sort_order TO Sort;

-- cert_doc_table_def：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_doc_table_def DROP COLUMN creator;
ALTER TABLE cert_doc_table_def DROP COLUMN modifier;
ALTER TABLE cert_doc_table_def DROP COLUMN deleter;
ALTER TABLE cert_doc_table_def RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_doc_table_def RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_doc_table_def RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_doc_table_def RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_doc_table_def RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_doc_table_def RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_doc_table_def RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_doc_table_def RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_table_def RENAME COLUMN sort_order TO Sort;

-- cert_doc_table_field_def：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_doc_table_field_def DROP COLUMN creator;
ALTER TABLE cert_doc_table_field_def DROP COLUMN modifier;
ALTER TABLE cert_doc_table_field_def DROP COLUMN deleter;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN sort_order TO Sort;

-- cert_enterprise：仅 create_by/update_by
ALTER TABLE cert_enterprise RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_enterprise RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_enterprise RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_enterprise RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_enterprise RENAME COLUMN remark TO Remark;
ALTER TABLE cert_enterprise RENAME COLUMN org_code TO OrgCode;

-- cert_file_requirement：只有 creator/modifier/deleter
ALTER TABLE cert_file_requirement RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_file_requirement RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_file_requirement RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_file_requirement RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_file_requirement RENAME COLUMN org_code TO OrgCode;

-- cert_iso_clause：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_iso_clause DROP COLUMN creator;
ALTER TABLE cert_iso_clause DROP COLUMN modifier;
ALTER TABLE cert_iso_clause DROP COLUMN deleter;
ALTER TABLE cert_iso_clause RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_iso_clause RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_iso_clause RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_iso_clause RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_iso_clause RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_iso_clause RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_iso_clause RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_iso_clause RENAME COLUMN org_code TO OrgCode;

-- cert_iso_standard：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_iso_standard DROP COLUMN creator;
ALTER TABLE cert_iso_standard DROP COLUMN modifier;
ALTER TABLE cert_iso_standard DROP COLUMN deleter;
ALTER TABLE cert_iso_standard RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_iso_standard RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_iso_standard RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_iso_standard RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_iso_standard RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_iso_standard RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_iso_standard RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_iso_standard RENAME COLUMN org_code TO OrgCode;

-- cert_message：仅 create_date/enable/remark
ALTER TABLE cert_message RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_message RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_message RENAME COLUMN remark TO Remark;

-- cert_org_stage：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
-- 注意：org_code 是业务列但按规范也改名
ALTER TABLE cert_org_stage DROP COLUMN creator;
ALTER TABLE cert_org_stage DROP COLUMN modifier;
ALTER TABLE cert_org_stage DROP COLUMN deleter;
ALTER TABLE cert_org_stage RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_org_stage RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_org_stage RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_org_stage RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_org_stage RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_org_stage RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_org_stage RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_org_stage RENAME COLUMN remark TO Remark;
-- org_code 是业务列，改名 OrgCode 但不能映射到 BaseEntity（BaseEntity 无此属性）
-- 因为 OrgCode 是具体业务字段，由子类声明
ALTER TABLE cert_org_stage RENAME COLUMN org_code TO OrgCode;

-- cert_org_standard：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_org_standard DROP COLUMN creator;
ALTER TABLE cert_org_standard DROP COLUMN modifier;
ALTER TABLE cert_org_standard DROP COLUMN deleter;
ALTER TABLE cert_org_standard RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_org_standard RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_org_standard RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_org_standard RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_org_standard RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_org_standard RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_org_standard RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_org_standard RENAME COLUMN remark TO Remark;
ALTER TABLE cert_org_standard RENAME COLUMN org_code TO OrgCode;

-- cert_phase_definition：使用独立命名约定（create_time/create_by/update_time/update_by/delete_time/delete_by/is_deleted/is_valid）
ALTER TABLE cert_phase_definition RENAME COLUMN create_time TO CreateTime;
ALTER TABLE cert_phase_definition RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_phase_definition RENAME COLUMN update_time TO UpdateTime;
ALTER TABLE cert_phase_definition RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_phase_definition RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_phase_definition RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_phase_definition RENAME COLUMN is_deleted TO IsDeleted;
ALTER TABLE cert_phase_definition RENAME COLUMN is_valid TO IsValid;

-- cert_report_template：只有 creator/modifier/deleter
ALTER TABLE cert_report_template RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_report_template RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_report_template RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_report_template RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_report_template RENAME COLUMN org_code TO OrgCode;

-- cert_standard_directory_config：只有 creator/modifier/deleter
ALTER TABLE cert_standard_directory_config RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_standard_directory_config RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_standard_directory_config RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_standard_directory_config RENAME COLUMN enable TO IsValid;

-- cert_standard_directory_file：只有 creator/modifier/deleter
ALTER TABLE cert_standard_directory_file RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_standard_directory_file RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_standard_directory_file RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_standard_directory_file RENAME COLUMN enable TO IsValid;

-- cert_standard_directory_folder：只有 creator/modifier/deleter
ALTER TABLE cert_standard_directory_folder RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_standard_directory_folder RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_standard_directory_folder RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_standard_directory_folder RENAME COLUMN enable TO IsValid;

-- cert_standard_phase_config：只有 creator/modifier/deleter
ALTER TABLE cert_standard_phase_config RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_standard_phase_config RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_standard_phase_config RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_standard_phase_config RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_standard_phase_config RENAME COLUMN org_code TO OrgCode;

-- cert_sys_config：仅 create_date/modify_date/enable/remark/sort_order
ALTER TABLE cert_sys_config RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_sys_config RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_sys_config RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_sys_config RENAME COLUMN remark TO Remark;
ALTER TABLE cert_sys_config RENAME COLUMN sort_order TO Sort;

-- cert_upload_task：仅 creator
ALTER TABLE cert_upload_task RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_upload_task RENAME COLUMN CreateDate TO CreateTime;
ALTER TABLE cert_upload_task RENAME COLUMN ModifyDate TO UpdateTime;

-- cert_validation_rule：有 creator/create_by, modifier/update_by, deleter/delete_by 重复
ALTER TABLE cert_validation_rule DROP COLUMN creator;
ALTER TABLE cert_validation_rule DROP COLUMN modifier;
ALTER TABLE cert_validation_rule DROP COLUMN deleter;
ALTER TABLE cert_validation_rule RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_validation_rule RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_validation_rule RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_validation_rule RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_validation_rule RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_validation_rule RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_validation_rule RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_validation_rule RENAME COLUMN org_code TO OrgCode;

-- cert_validation_rule_source：只有 creator/modifier/deleter
ALTER TABLE cert_validation_rule_source RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_validation_rule_source RENAME COLUMN modifier TO UpdateBy;
ALTER TABLE cert_validation_rule_source RENAME COLUMN deleter TO DeleteBy;
ALTER TABLE cert_validation_rule_source RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_validation_rule_source RENAME COLUMN org_code TO OrgCode;

-- ============================================================
-- 第三十一步：重建视图（使用新列名）
-- ============================================================

-- v_cert_phase_definition
CREATE OR REPLACE VIEW v_cert_phase_definition AS
SELECT
    p.id AS id,
    p.code AS code,
    p.phase_code AS phase_code,
    p.phase_name AS phase_name,
    p.sequence_order AS sequence_order,
    p.description AS description,
    p.IsValid AS IsValid,
    p.CreateTime AS CreateTime,
    p.CreateBy AS CreateBy,
    p.UpdateTime AS UpdateTime,
    p.UpdateBy AS UpdateBy,
    p.DeleteTime AS DeleteTime,
    p.DeleteBy AS DeleteBy,
    p.IsDeleted AS IsDeleted,
    CASE p.IsValid WHEN 1 THEN '启用' ELSE '停用' END AS status_name
FROM cert_phase_definition p
WHERE p.IsDeleted = 0;

-- v_iso_standard（使用新列名）
CREATE OR REPLACE VIEW v_iso_standard AS
SELECT
    s.Id AS Id,
    s.Code AS Code,
    s.OrgCode AS OrgCode,
    s.cb_code AS cb_code,
    cb.short_name AS CbName,
    s.standard_code AS standard_code,
    s.standard_name AS standard_name,
    s.version_year AS version_year,
    s.category AS category,
    cat.DicName AS CategoryName,
    s.description AS description,
    s.status AS status,
    CASE s.status WHEN 'active' THEN '启用' WHEN 'inactive' THEN '停用' ELSE s.status END AS StatusName,
    s.CreateBy AS CreateBy,
    s.CreateTime AS CreateTime,
    s.UpdateBy AS UpdateBy,
    s.UpdateTime AS UpdateTime,
    s.DeleteBy AS DeleteBy,
    s.DeleteTime AS DeleteTime,
    s.IsValid AS IsValid,
    s.Sort AS Sort,
    s.Remark AS Remark
FROM cert_iso_standard s
LEFT JOIN cert_certification_body cb ON s.cb_code = cb.Code
LEFT JOIN sys_dictionarylist cat ON cat.DicCode = 'iso_category' AND cat.DicValue = s.category;

-- v_certification_body
CREATE OR REPLACE VIEW v_certification_body AS
SELECT
    cb.Id AS Id,
    cb.Code AS Code,
    cb.OrgCode AS OrgCode,
    cb.status AS status,
    CASE cb.status WHEN 'active' THEN '启用' ELSE cb.status END AS StatusName,
    cb.IsValid AS IsValid,
    cb.Sort AS Sort,
    cb.Remark AS Remark,
    cb.CreateBy AS CreateBy,
    cb.CreateTime AS CreateTime,
    cb.UpdateBy AS UpdateBy,
    cb.UpdateTime AS UpdateTime,
    cb.DeleteBy AS DeleteBy,
    cb.DeleteTime AS DeleteTime,
    cb.name AS name,
    cb.short_name AS short_name,
    cb.cb_code AS cb_code,
    cb.contact_name AS contact_name,
    cb.contact_phone AS contact_phone,
    cb.legal_person AS legal_person,
    cb.contact_email AS contact_email,
    cb.address AS address,
    cb.logo_url AS logo_url,
    cb.scope_text AS scope_text,
    cb.theme_config AS theme_config,
    cb.login_config AS login_config,
    cb.max_users AS max_users,
    cb.max_enterprises AS max_enterprises,
    cb.expire_date AS expire_date
FROM cert_certification_body cb;

-- v_workflow（wf_workflow_definition 不在 cert_* 范围，但其视图引用了列）
-- 注意：此视图不在改名范围内（表本身不改），仅当需要时手动更新

SET FOREIGN_KEY_CHECKS = 1;
