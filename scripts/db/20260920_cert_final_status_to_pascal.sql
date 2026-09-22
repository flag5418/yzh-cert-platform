-- 最终收尾：剩余 status → Status（保持 NULL，避免 data truncation）
USE yzh_cert_platform;

ALTER TABLE cert_application        CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
ALTER TABLE cert_clause_extraction_rule CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
ALTER TABLE cert_directory_template CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
ALTER TABLE cert_file_requirement   CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
ALTER TABLE cert_report_template    CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
ALTER TABLE cert_standard_directory_config CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
ALTER TABLE cert_standard_directory_file CHANGE COLUMN status Status varchar(20) DEFAULT 'active';
ALTER TABLE cert_standard_directory_folder CHANGE COLUMN status Status varchar(20) DEFAULT 'active';
ALTER TABLE cert_standard_phase_config CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
ALTER TABLE cert_validation_rule    CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
ALTER TABLE cert_validation_rule_source CHANGE COLUMN status Status varchar(20) DEFAULT 'none';
