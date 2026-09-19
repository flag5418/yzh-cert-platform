-- ========================================================================
-- 2026-09-19: 补齐缺失的 IsDeleted / IsValid 列（标准化审计字段）
-- 前提：EntityBase 不能删除（仍有 Dir/Sys 系列旧实体在用）
-- 策略：补齐所有业务表的标准列，使架构统一
-- ========================================================================

-- ===== 一、缺 IsValid 的表（27 表，已有 IsDeleted） =====

ALTER TABLE `audit_checklist_item` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `audit_evidence` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `audit_finding` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `audit_nonconformity` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `audit_project` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `audit_rectification` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `cert_application` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `cert_enterprise` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `cert_upload_task` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `ent_enterprise_document` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `ent_enterprise_file` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `ent_enterprise_phase` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `ent_extraction_result` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `ent_file_compliance_check` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `ent_file_pre_check_result` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `ent_file_version` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `ent_table_extraction_result` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `rpt_audit_report` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `rpt_report_section` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `rpt_report_section_source` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `rpt_report_task` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `wf_execution_task` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `wf_execution_task_item` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `wf_field_label_mapping` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `wf_node_execution` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `wf_prompt_template` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `wf_skill_api` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `wf_workflow_definition` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;
ALTER TABLE `wf_workflow_execution_log` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1 AFTER `IsDeleted`;

-- ===== 二、缺 IsDeleted + IsValid 的表（4 表，yzh_* 系列） =====

-- 注意：这些表没有标准审计列，需要先加 IsDeleted 再放 IsValid
-- 审计字段追加在表最后一个列之后

ALTER TABLE `yzh_field_config` ADD COLUMN `IsDeleted` bool NOT NULL DEFAULT 0;
ALTER TABLE `yzh_field_config` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1;

ALTER TABLE `yzh_page_config` ADD COLUMN `IsDeleted` bool NOT NULL DEFAULT 0;
ALTER TABLE `yzh_page_config` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1;

ALTER TABLE `yzh_queue` ADD COLUMN `IsDeleted` bool NOT NULL DEFAULT 0;
ALTER TABLE `yzh_queue` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1;

ALTER TABLE `yzh_queue_resource_lock` ADD COLUMN `IsDeleted` bool NOT NULL DEFAULT 0;
ALTER TABLE `yzh_queue_resource_lock` ADD COLUMN `IsValid` int NOT NULL DEFAULT 1;
