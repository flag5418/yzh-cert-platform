-- 修复：剩余 cert_* 系列表中 snake_case 列改为 PascalCase
-- 状态：cert_doc_extraction_rule + cert_ai_usage_log + 部分 audit 列已更新
USE yzh_cert_platform;

-- 先修补可能存在的 NULL 值
UPDATE cert_report_template SET status = 'none' WHERE status IS NULL;

-- 只在列仍是 snake_case 时改名
ALTER TABLE cert_doc_field_def      CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(100) NOT NULL, CHANGE COLUMN description Description text NULL, CHANGE COLUMN status Status varchar(50) NOT NULL DEFAULT 'active';
ALTER TABLE cert_doc_table_def      CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(100) NOT NULL, CHANGE COLUMN description Description text NULL, CHANGE COLUMN status Status varchar(50) NOT NULL DEFAULT 'active';
ALTER TABLE cert_doc_table_field_def CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(100) NOT NULL, CHANGE COLUMN status Status varchar(50) NOT NULL DEFAULT 'active';
ALTER TABLE cert_ai_config          CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(100) NOT NULL, CHANGE COLUMN provider Provider varchar(50) NOT NULL, CHANGE COLUMN model Model varchar(50) NOT NULL, CHANGE COLUMN temperature Temperature float NOT NULL DEFAULT 0.7, CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_message            CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(100) NOT NULL, CHANGE COLUMN title Title varchar(200) NOT NULL, CHANGE COLUMN content Content text NULL, CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none', CHANGE COLUMN related_code RelatedCode varchar(100) NULL;
ALTER TABLE cert_org_stage          CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(100) NOT NULL, CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_org_standard       CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(100) NOT NULL, CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_upload_task        CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(64) NOT NULL, CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'initialized';
ALTER TABLE cert_validation_rule    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_validation_rule_source CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT, CHANGE COLUMN code Code varchar(100) NOT NULL, CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_application        CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_clause_extraction_rule CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_directory_template CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_file_requirement   CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_report_template    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_standard_directory_config CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_standard_phase_config CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_standard_directory_file CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'active';
ALTER TABLE cert_standard_directory_folder CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'active';
ALTER TABLE cert_report_template    CHANGE COLUMN is_default IsDefault tinyint(1) NOT NULL DEFAULT 0, CHANGE COLUMN template_file_path TemplateFilePath varchar(500) NULL, CHANGE COLUMN section_config SectionConfig json NULL;
