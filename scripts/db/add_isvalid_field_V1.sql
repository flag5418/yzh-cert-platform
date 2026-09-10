-- ============================================================================
-- 脚本：add_isvalid_field_V1.sql
-- 功能：为所有业务表添加 IsValid 有效标志字段（1=有效，0=无效）
-- 日期：2026-09-10
-- 说明：YZH Core 架构升级 - 统一有效标志字段，替代 Enable 字段
-- 状态：⏳ 待执行
-- ============================================================================

-- ==================== 系统管理表 ====================

-- Sys_User（用户表）
ALTER TABLE Sys_User ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;
CREATE INDEX IX_Sys_User_IsValid ON Sys_User (IsValid);

-- Sys_Organization（机构表）
ALTER TABLE Sys_Organization ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;
CREATE INDEX IX_Sys_Organization_IsValid ON Sys_Organization (IsValid);

-- Sys_Role（角色表）
ALTER TABLE Sys_Role ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;
CREATE INDEX IX_Sys_Role_IsValid ON Sys_Role (IsValid);

-- Sys_Menu（菜单表）
ALTER TABLE Sys_Menu ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;
CREATE INDEX IX_Sys_Menu_IsValid ON Sys_Menu (IsValid);

-- Sys_Dictionary（字典表）
ALTER TABLE Sys_Dictionary ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;
CREATE INDEX IX_Sys_Dictionary_IsValid ON Sys_Dictionary (IsValid);

-- Sys_DictionaryList（字典明细表）
ALTER TABLE Sys_DictionaryList ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;
CREATE INDEX IX_Sys_DictionaryList_IsValid ON Sys_DictionaryList (IsValid);

-- Sys_RoleAuth（角色权限表）
ALTER TABLE Sys_RoleAuth ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- Sys_UserDepartment（用户部门表）
ALTER TABLE Sys_UserDepartment ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- Sys_Log（日志表）
ALTER TABLE sys_log ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ==================== 认证业务表 ====================

-- cert_iso_standard（ISO标准表）
ALTER TABLE cert_iso_standard ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_iso_clause（ISO条款表）
ALTER TABLE cert_iso_clause ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_cert_stage（认证阶段表）
ALTER TABLE cert_cert_stage ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_phase_definition（阶段定义表）
ALTER TABLE cert_phase_definition ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_org_stage（机构阶段表）
ALTER TABLE cert_org_stage ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_org_standard（机构标准表）
ALTER TABLE cert_org_standard ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_auditor_profile（审核员档案表）
ALTER TABLE cert_auditor_profile ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_report_template（报告模板表）
ALTER TABLE cert_report_template ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_file_requirement（文件要求表）
ALTER TABLE cert_file_requirement ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_directory_template（目录模板表）
ALTER TABLE cert_directory_template ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_validation_rule（验证规则表）
ALTER TABLE cert_validation_rule ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_extraction_rule（提取规则表）
ALTER TABLE cert_extraction_rule ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_standard_directory_config（标准目录配置表）
ALTER TABLE cert_standard_directory_config ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_standard_directory_folder（标准目录文件夹表）
ALTER TABLE cert_standard_directory_folder ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_standard_directory_file（标准目录文件表）
ALTER TABLE cert_standard_directory_file ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_upload_task（上传任务表）
ALTER TABLE cert_upload_task ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_message（消息表）
ALTER TABLE cert_message ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_certification_body（认证机构表）
ALTER TABLE cert_certification_body ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_standard_phase_config（标准阶段配置表）
ALTER TABLE cert_standard_phase_config ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- cert_sys_config（系统配置表）
ALTER TABLE cert_sys_config ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ==================== 企业相关表 ====================

-- ent_enterprise（企业表）
ALTER TABLE ent_enterprise ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ent_enterprise_file（企业文件表）
ALTER TABLE ent_enterprise_file ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ent_enterprise_document（企业文档表）
ALTER TABLE ent_enterprise_document ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ent_file_version（文件版本表）
ALTER TABLE ent_file_version ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ent_extraction_result（提取结果表）
ALTER TABLE ent_extraction_result ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ent_table_extraction_result（表格提取结果表）
ALTER TABLE ent_table_extraction_result ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ==================== 审核相关表 ====================

-- audit_task（审核任务表）
ALTER TABLE audit_task ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- rpt_audit_report（审核报告表）
ALTER TABLE rpt_audit_report ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- rpt_report_section（报告章节表）
ALTER TABLE rpt_report_section ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ==================== 工作流相关表 ====================

-- wf_workflow_definition（工作流定义表）
ALTER TABLE wf_workflow_definition ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_skill（技能表）
ALTER TABLE wf_skill ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_skill_category（技能分类表）
ALTER TABLE wf_skill_category ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_skill_input（技能输入表）
ALTER TABLE wf_skill_input ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_skill_output（技能输出表）
ALTER TABLE wf_skill_output ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_skill_reflection（技能反射表）
ALTER TABLE wf_skill_reflection ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_execution_task（执行任务表）
ALTER TABLE wf_execution_task ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_execution_task_item（执行任务明细表）
ALTER TABLE wf_execution_task_item ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_node_execution（节点执行表）
ALTER TABLE wf_node_execution ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- wf_prompt_template（提示词模板表）
ALTER TABLE wf_prompt_template ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ==================== 配置表 ====================

-- yzh_field_config（字段配置表）
ALTER TABLE yzh_field_config ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- yzh_page_config（页面配置表）
ALTER TABLE yzh_page_config ADD COLUMN IsValid INT NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）' AFTER IsDeleted;

-- ==================== 完成提示 ====================
-- 所有表已添加 IsValid 字段，默认值为 1（有效）
-- 现有数据自动变为有效状态
-- 前端可通过 toggle-valid 接口切换状态
-- 查询自动过滤 IsValid=0 的记录
