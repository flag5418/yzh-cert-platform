-- ============================================================
-- 脚本：cert_* 系列表审计列 snake_case → PascalCase
-- 日期：2026-09-20
-- 架构：YZH.Core.BaseEntity 使用 PascalCase，应将 DB 列同步为 PascalCase
-- 依赖：yzh_* 系列已在 20260919_queue_snake_to_pascal_V1.sql 修复；ISO/Cert 系列已 PascalCase
-- 状态：cert_doc_extraction_rule 已通过首次执行部分更新（Id, Code, Skill, Status, CreateTime, CreateBy, UpdateTime, UpdateBy, DeleteTime, DeleteBy）
--       本脚本处理剩余列和其他表
-- ============================================================

USE yzh_cert_platform;

-- ────────────────────────────────────────────────────────────
-- cert_doc_extraction_rule — 补完 prompt 列
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_doc_extraction_rule
    CHANGE COLUMN prompt Prompt text NULL;

-- ────────────────────────────────────────────────────────────
-- cert_doc_field_def — 字段定义
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_doc_field_def
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN description Description text NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN create_time CreateTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL,
    CHANGE COLUMN update_time UpdateTime datetime NULL ON UPDATE CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL;

-- ────────────────────────────────────────────────────────────
-- cert_doc_table_def — 表格定义
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_doc_table_def
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN description Description text NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN create_time CreateTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL,
    CHANGE COLUMN update_time UpdateTime datetime NULL ON UPDATE CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL;

-- ────────────────────────────────────────────────────────────
-- cert_doc_table_field_def — 表格字段定义
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_doc_table_field_def
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN table_code TableCode varchar(100) NOT NULL,
    CHANGE COLUMN create_time CreateTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL,
    CHANGE COLUMN update_time UpdateTime datetime NULL ON UPDATE CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL;

-- ────────────────────────────────────────────────────────────
-- cert_ai_config — AI 配置
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_ai_config
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN provider Provider varchar(50) NOT NULL,
    CHANGE COLUMN model Model varchar(50) NOT NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN temperature Temperature float NOT NULL DEFAULT 0.7,
    CHANGE COLUMN api_key ApiKey varchar(500) NOT NULL,
    CHANGE COLUMN max_tokens MaxTokens int NOT NULL DEFAULT 4096,
    CHANGE COLUMN create_time CreateTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL,
    CHANGE COLUMN update_time UpdateTime datetime NULL ON UPDATE CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL;

-- ────────────────────────────────────────────────────────────
-- cert_ai_usage_log — AI 使用日志
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_ai_usage_log
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN provider Provider varchar(50) NOT NULL,
    CHANGE COLUMN model Model varchar(50) NOT NULL,
    CHANGE COLUMN skill Skill varchar(50) NOT NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN success Success tinyint(1) NOT NULL DEFAULT 1,
    CHANGE COLUMN prompt_tokens PromptTokens int NOT NULL DEFAULT 0,
    CHANGE COLUMN completion_tokens CompletionTokens int NOT NULL DEFAULT 0,
    CHANGE COLUMN total_tokens TotalTokens int NOT NULL DEFAULT 0,
    CHANGE COLUMN cost_usd CostUsd decimal(10,6) NOT NULL DEFAULT 0,
    CHANGE COLUMN duration_ms DurationMs int NOT NULL DEFAULT 0,
    CHANGE COLUMN error_message ErrorMessage text NULL,
    CHANGE COLUMN business_ref BusinessRef varchar(200) NULL,
    CHANGE COLUMN business_type BusinessType varchar(100) NULL,
    CHANGE COLUMN call_id CallId varchar(100) NULL,
    CHANGE COLUMN create_time CreateTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL,
    CHANGE COLUMN update_time UpdateTime datetime NULL ON UPDATE CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL;

-- ────────────────────────────────────────────────────────────
-- cert_message — 系统消息
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_message
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN title Title varchar(200) NOT NULL,
    CHANGE COLUMN content Content text NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN is_read IsRead tinyint(1) NOT NULL DEFAULT 0,
    CHANGE COLUMN read_date ReadDate datetime NULL,
    CHANGE COLUMN related_code RelatedCode varchar(100) NULL,
    CHANGE COLUMN message_type MessageType varchar(50) NOT NULL,
    CHANGE COLUMN extra_data ExtraData json NULL,
    CHANGE COLUMN create_time CreateTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL,
    CHANGE COLUMN update_time UpdateTime datetime NULL ON UPDATE CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL;

-- ────────────────────────────────────────────────────────────
-- cert_org_stage — 机构阶段
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_org_stage
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN stage_code StageCode varchar(100) NOT NULL,
    CHANGE COLUMN create_time CreateTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL,
    CHANGE COLUMN update_time UpdateTime datetime NULL ON UPDATE CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL;

-- ────────────────────────────────────────────────────────────
-- cert_org_standard — 机构标准
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_org_standard
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN create_time CreateTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL,
    CHANGE COLUMN update_time UpdateTime datetime NULL ON UPDATE CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL;

-- ────────────────────────────────────────────────────────────
-- cert_upload_task — 上传任务
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_upload_task
    CHANGE COLUMN code Code varchar(64) NOT NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'initialized',
    CHANGE COLUMN total_size TotalSize bigint NOT NULL DEFAULT 0,
    CHANGE COLUMN create_by CreateBy varchar(64) NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL;

-- ────────────────────────────────────────────────────────────
-- cert_validation_rule — 验证规则
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_validation_rule
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN rule_name RuleName varchar(200) NOT NULL,
    CHANGE COLUMN rule_name_en RuleNameEn varchar(200) NULL,
    CHANGE COLUMN is_active IsActive tinyint(1) NOT NULL DEFAULT 1,
    CHANGE COLUMN severity_if_violated SeverityIfViolated varchar(20) NOT NULL DEFAULT 'major',
    CHANGE COLUMN nc_description_template NcDescriptionTemplate text NULL,
    CHANGE COLUMN layout_json LayoutJson json NULL,
    CHANGE COLUMN rule_json RuleJson json NOT NULL,
    CHANGE COLUMN workflow_code WorkflowCode varchar(100) NOT NULL,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL;

-- ────────────────────────────────────────────────────────────
-- cert_validation_rule_source — 验证规则来源
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_validation_rule_source
    CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
    CHANGE COLUMN code Code varchar(100) NOT NULL,
    CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none',
    CHANGE COLUMN rule_code RuleCode varchar(100) NOT NULL,
    CHANGE COLUMN source_path SourcePath varchar(500) NULL,
    CHANGE COLUMN file_requirement_code FileRequirementCode varchar(100) NOT NULL,
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL;

-- ────────────────────────────────────────────────────────────
-- 其他表 status 列
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_application            CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_clause_extraction_rule CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_directory_template     CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_file_requirement       CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_report_template        CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_standard_directory_config CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_standard_phase_config  CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'none';
ALTER TABLE cert_standard_directory_file CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'active';
ALTER TABLE cert_standard_directory_folder CHANGE COLUMN status Status varchar(20) NOT NULL DEFAULT 'active';

-- ────────────────────────────────────────────────────────────
-- cert_report_template 其他列
-- ────────────────────────────────────────────────────────────
ALTER TABLE cert_report_template
    CHANGE COLUMN is_default IsDefault tinyint(1) NOT NULL DEFAULT 0,
    CHANGE COLUMN template_file_path TemplateFilePath varchar(500) NULL,
    CHANGE COLUMN section_config SectionConfig json NULL;
