-- ====================================================
-- 修复 wf_*/audit_*/ent_* 表的审计字段
-- 日期: 2026-09-13
-- 目标: 添加 IsDeleted 列（软删除支持）
-- 说明: 这些表使用 Vol 风格 snake_case 列名，仅添加 IsDeleted 不对原有列做改动
-- ====================================================

-- ────────────────────────────────────────────────────
-- audit_* 表（8 张）
-- ────────────────────────────────────────────────────
ALTER TABLE audit_checklist_item ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE audit_evidence        ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE audit_finding         ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE audit_nonconformity    ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE audit_project         ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE audit_rectification   ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE audit_task            ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;

-- ────────────────────────────────────────────────────
-- ent_* 表（10 张）
-- ────────────────────────────────────────────────────
ALTER TABLE ent_enterprise              ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE ent_enterprise_document    ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE ent_enterprise_file        ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE ent_enterprise_phase       ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE ent_extraction_result      ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE ent_file_compliance_check  ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE ent_file_pre_check_result  ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE ent_file_version           ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE ent_table_extraction_result ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;

-- ────────────────────────────────────────────────────
-- wf_* 表（9 张）
-- ────────────────────────────────────────────────────
ALTER TABLE wf_execution_task      ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_execution_task_item ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_field_label_mapping ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_node_execution      ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_prompt_template     ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_skill_api           ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_skill_category      ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_skill_input         ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_skill_output        ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_skill_reflection    ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_workflow_definition ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE wf_workflow_execution_log ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0;
