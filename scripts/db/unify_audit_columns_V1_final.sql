-- 最终修复：清理剩余旧列名（修正版）
SET FOREIGN_KEY_CHECKS = 0;

-- 处理 cert_doc_extraction_rule（is_valid 是业务列，需要改名保留）
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN is_valid TO DocIsValid;

-- 处理有 creator/modifier/deleter 需要 DROP 的表
ALTER TABLE cert_clause_extraction_rule DROP COLUMN creator;
ALTER TABLE cert_clause_extraction_rule DROP COLUMN modifier;
ALTER TABLE cert_clause_extraction_rule DROP COLUMN deleter;
ALTER TABLE cert_clause_extraction_rule RENAME COLUMN enable TO IsValid;

ALTER TABLE cert_directory_template DROP COLUMN creator;
ALTER TABLE cert_directory_template DROP COLUMN modifier;
ALTER TABLE cert_directory_template DROP COLUMN deleter;
ALTER TABLE cert_directory_template RENAME COLUMN enable TO IsValid;

ALTER TABLE cert_file_requirement DROP COLUMN creator;
ALTER TABLE cert_file_requirement DROP COLUMN modifier;
ALTER TABLE cert_file_requirement DROP COLUMN deleter;
ALTER TABLE cert_file_requirement RENAME COLUMN enable TO IsValid;

ALTER TABLE cert_report_template DROP COLUMN creator;
ALTER TABLE cert_report_template DROP COLUMN modifier;
ALTER TABLE cert_report_template DROP COLUMN deleter;
ALTER TABLE cert_report_template RENAME COLUMN enable TO IsValid;

ALTER TABLE cert_standard_directory_config DROP COLUMN creator;
ALTER TABLE cert_standard_directory_config DROP COLUMN modifier;
ALTER TABLE cert_standard_directory_config DROP COLUMN deleter;
ALTER TABLE cert_standard_directory_config RENAME COLUMN enable TO IsValid;

ALTER TABLE cert_standard_directory_file DROP COLUMN creator;
ALTER TABLE cert_standard_directory_file DROP COLUMN modifier;
ALTER TABLE cert_standard_directory_file DROP COLUMN deleter;
ALTER TABLE cert_standard_directory_file RENAME COLUMN enable TO IsValid;

ALTER TABLE cert_standard_directory_folder DROP COLUMN creator;
ALTER TABLE cert_standard_directory_folder DROP COLUMN modifier;
ALTER TABLE cert_standard_directory_folder DROP COLUMN deleter;
ALTER TABLE cert_standard_directory_folder RENAME COLUMN enable TO IsValid;

ALTER TABLE cert_standard_phase_config DROP COLUMN creator;
ALTER TABLE cert_standard_phase_config DROP COLUMN modifier;
ALTER TABLE cert_standard_phase_config DROP COLUMN deleter;
ALTER TABLE cert_standard_phase_config RENAME COLUMN enable TO IsValid;

ALTER TABLE cert_validation_rule_source DROP COLUMN creator;
ALTER TABLE cert_validation_rule_source DROP COLUMN modifier;
ALTER TABLE cert_validation_rule_source DROP COLUMN deleter;
ALTER TABLE cert_validation_rule_source RENAME COLUMN enable TO IsValid;

-- 处理 remark → Remark
ALTER TABLE cert_doc_extraction_rule RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_field_def RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_table_def RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN remark TO Remark;

SET FOREIGN_KEY_CHECKS = 1;
