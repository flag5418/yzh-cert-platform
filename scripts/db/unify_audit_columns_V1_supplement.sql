-- 补充执行：各表审计字段改名（续）
SET FOREIGN_KEY_CHECKS = 0;

ALTER TABLE cert_ai_config DROP COLUMN modifier;
ALTER TABLE cert_ai_config DROP COLUMN deleter;
ALTER TABLE cert_ai_config DROP COLUMN update_date;
ALTER TABLE cert_ai_config RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_ai_config RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_ai_config RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_ai_config RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_ai_config RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_ai_config RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_ai_config RENAME COLUMN sort TO Sort;
ALTER TABLE cert_ai_config RENAME COLUMN remark TO Remark;
ALTER TABLE cert_ai_config RENAME COLUMN org_code TO OrgCode;

ALTER TABLE cert_ai_usage_log RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_ai_usage_log RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_ai_usage_log RENAME COLUMN remark TO Remark;

ALTER TABLE cert_application RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_application RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_application RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_application RENAME COLUMN modify_date TO UpdateTime;

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
ALTER TABLE cert_doc_field_def RENAME COLUMN sort_order TO Sort;

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
ALTER TABLE cert_doc_table_def RENAME COLUMN sort_order TO Sort;

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
ALTER TABLE cert_doc_table_field_def RENAME COLUMN sort_order TO Sort;

ALTER TABLE cert_enterprise RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_enterprise RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_enterprise RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_enterprise RENAME COLUMN modify_date TO UpdateTime;

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

ALTER TABLE cert_message RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_message RENAME COLUMN enable TO IsValid;

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
ALTER TABLE cert_org_stage RENAME COLUMN org_code TO OrgCode;

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

ALTER TABLE cert_phase_definition RENAME COLUMN create_time TO CreateTime;
ALTER TABLE cert_phase_definition RENAME COLUMN create_by TO CreateBy;
ALTER TABLE cert_phase_definition RENAME COLUMN update_time TO UpdateTime;
ALTER TABLE cert_phase_definition RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE cert_phase_definition RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE cert_phase_definition RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE cert_phase_definition RENAME COLUMN is_deleted TO IsDeleted;
ALTER TABLE cert_phase_definition RENAME COLUMN is_valid TO IsValid;

ALTER TABLE cert_sys_config RENAME COLUMN create_date TO CreateTime;
ALTER TABLE cert_sys_config RENAME COLUMN modify_date TO UpdateTime;
ALTER TABLE cert_sys_config RENAME COLUMN enable TO IsValid;
ALTER TABLE cert_sys_config RENAME COLUMN remark TO Remark;
ALTER TABLE cert_sys_config RENAME COLUMN sort_order TO Sort;

ALTER TABLE cert_upload_task RENAME COLUMN creator TO CreateBy;
ALTER TABLE cert_upload_task RENAME COLUMN CreateDate TO CreateTime;
ALTER TABLE cert_upload_task RENAME COLUMN ModifyDate TO UpdateTime;

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

SET FOREIGN_KEY_CHECKS = 1;
