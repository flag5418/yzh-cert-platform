-- ========================================================================
-- 2026-09-19 V2: 全库审计字段统一为 PascalCase（精确版）
-- 
-- 铁律：DB列名 = C#属性名 = PascalCase
-- 统一标准：CreateBy, CreateTime, UpdateBy, UpdateTime, DeleteBy, DeleteTime
-- ========================================================================

SET SQL_SAFE_UPDATES = 0;

-- ========================================================================
-- 一、audit_* 系列
-- ========================================================================

-- audit_checklist_item: CreateBy+creator 并存
UPDATE audit_checklist_item SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE audit_checklist_item SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE audit_checklist_item SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE audit_checklist_item DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE audit_checklist_item CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE audit_checklist_item CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- audit_evidence: CreateBy+creator 并存
UPDATE audit_evidence SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE audit_evidence SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE audit_evidence SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE audit_evidence DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE audit_evidence CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE audit_evidence CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- audit_finding: CreateBy+creator 并存
UPDATE audit_finding SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE audit_finding SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE audit_finding SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE audit_finding DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE audit_finding CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE audit_finding CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- audit_nonconformity: CreateBy+creator 并存
UPDATE audit_nonconformity SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE audit_nonconformity SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE audit_nonconformity SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE audit_nonconformity DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE audit_nonconformity CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE audit_nonconformity CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- audit_project: 全小写
ALTER TABLE audit_project
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL;

-- audit_rectification: CreateBy+creator 并存
UPDATE audit_rectification SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE audit_rectification SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE audit_rectification SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE audit_rectification DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE audit_rectification CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE audit_rectification CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- ========================================================================
-- 二、ent_* 系列（8张表，全部 CreateBy+creator 并存）
-- ========================================================================

UPDATE ent_enterprise_document SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE ent_enterprise_document SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE ent_enterprise_document SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE ent_enterprise_document DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE ent_enterprise_document CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE ent_enterprise_document CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

UPDATE ent_enterprise_file SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE ent_enterprise_file SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE ent_enterprise_file SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE ent_enterprise_file DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE ent_enterprise_file CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE ent_enterprise_file CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

UPDATE ent_enterprise_phase SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE ent_enterprise_phase SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE ent_enterprise_phase SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE ent_enterprise_phase DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE ent_enterprise_phase CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE ent_enterprise_phase CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

UPDATE ent_extraction_result SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE ent_extraction_result SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE ent_extraction_result SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE ent_extraction_result DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE ent_extraction_result CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE ent_extraction_result CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

UPDATE ent_file_compliance_check SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE ent_file_compliance_check SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE ent_file_compliance_check SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE ent_file_compliance_check DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE ent_file_compliance_check CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE ent_file_compliance_check CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

UPDATE ent_file_pre_check_result SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE ent_file_pre_check_result SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE ent_file_pre_check_result SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE ent_file_pre_check_result DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE ent_file_pre_check_result CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE ent_file_pre_check_result CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

UPDATE ent_file_version SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE ent_file_version SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE ent_file_version SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE ent_file_version DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE ent_file_version CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE ent_file_version CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

UPDATE ent_table_extraction_result SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE ent_table_extraction_result SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE ent_table_extraction_result SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE ent_table_extraction_result DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE ent_table_extraction_result CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE ent_table_extraction_result CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- ========================================================================
-- 三、rpt_* 系列（4张表）
-- ========================================================================

UPDATE rpt_audit_report SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE rpt_audit_report SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE rpt_audit_report SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE rpt_audit_report DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE rpt_audit_report CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE rpt_audit_report CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- rpt_report_section: 全小写 C 类（9列混合）
UPDATE rpt_report_section SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE rpt_report_section SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE rpt_report_section SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE rpt_report_section
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE rpt_report_section DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

UPDATE rpt_report_section_source SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE rpt_report_section_source SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE rpt_report_section_source SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE rpt_report_section_source DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE rpt_report_section_source CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE rpt_report_section_source CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

UPDATE rpt_report_task SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE rpt_report_task SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE rpt_report_task SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE rpt_report_task DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE rpt_report_task CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE rpt_report_task CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- ========================================================================
-- 四、wf_* 系列（10张表）
-- ========================================================================

-- wf_execution_task: 全小写 C 类
UPDATE wf_execution_task SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_execution_task SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_execution_task SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_execution_task
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE wf_execution_task DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- wf_execution_task_item
UPDATE wf_execution_task_item SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_execution_task_item SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_execution_task_item SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_execution_task_item
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE wf_execution_task_item DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- wf_field_label_mapping: CreateBy+creator 并存
UPDATE wf_field_label_mapping SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE wf_field_label_mapping SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_field_label_mapping SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_field_label_mapping DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE wf_field_label_mapping CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE wf_field_label_mapping CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- wf_node_execution: 全小写 C 类
UPDATE wf_node_execution SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_node_execution SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_node_execution SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_node_execution
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE wf_node_execution DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- wf_skill_api
UPDATE wf_skill_api SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_skill_api SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_skill_api SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_skill_api
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE wf_skill_api DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- wf_skill_input
UPDATE wf_skill_input SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_skill_input SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_skill_input SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_skill_input
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE wf_skill_input DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- wf_skill_output
UPDATE wf_skill_output SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_skill_output SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_skill_output SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_skill_output
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE wf_skill_output DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- wf_skill_reflection
UPDATE wf_skill_reflection SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_skill_reflection SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_skill_reflection SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_skill_reflection
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE wf_skill_reflection DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- wf_workflow_definition
UPDATE wf_workflow_definition SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_workflow_definition SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_workflow_definition SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_workflow_definition
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE wf_workflow_definition DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- wf_workflow_execution_log: CreateBy+creator 并存
UPDATE wf_workflow_execution_log SET CreateBy = COALESCE(CreateBy, creator) WHERE creator IS NOT NULL;
UPDATE wf_workflow_execution_log SET UpdateBy = COALESCE(UpdateBy, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_workflow_execution_log SET DeleteBy = COALESCE(DeleteBy, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE wf_workflow_execution_log DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;
ALTER TABLE wf_workflow_execution_log CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE wf_workflow_execution_log CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- ========================================================================
-- 五、yzh_* 系列（3张表，全小写 C 类）
-- ========================================================================

UPDATE yzh_queue SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE yzh_queue SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE yzh_queue SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE yzh_queue
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE yzh_queue DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

UPDATE yzh_queue_resource_lock SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE yzh_queue_resource_lock SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE yzh_queue_resource_lock SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE yzh_queue_resource_lock
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE yzh_queue_resource_lock DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

UPDATE yzh_queue_task SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE yzh_queue_task SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE yzh_queue_task SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;
ALTER TABLE yzh_queue_task
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL;
ALTER TABLE yzh_queue_task DROP COLUMN creator, DROP COLUMN modifier, DROP COLUMN deleter;

-- ========================================================================
-- 六、旧 Vol 框架表
-- ========================================================================

-- FormCollectionObject
ALTER TABLE FormCollectionObject
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE FormCollectionObject DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- FormDesignOptions
ALTER TABLE FormDesignOptions
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE FormDesignOptions DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- SellOrder
ALTER TABLE SellOrder
    CHANGE COLUMN Creator CreateBy varchar(255) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(255) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE SellOrder DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- SellOrderList
ALTER TABLE SellOrderList
    CHANGE COLUMN Creator CreateBy varchar(255) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(255) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE SellOrderList DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_QuartzLog
ALTER TABLE Sys_QuartzLog
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE Sys_QuartzLog DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_QuartzOptions
ALTER TABLE Sys_QuartzOptions
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE Sys_QuartzOptions DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_RoleAuth（Creator/Modifier 是 text 类型）
ALTER TABLE Sys_RoleAuth
    CHANGE COLUMN Creator CreateBy text NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy text NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- Sys_TableColumn（Modifier 是 longtext）
ALTER TABLE Sys_TableColumn
    CHANGE COLUMN Creator CreateBy varchar(200) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy longtext NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE Sys_TableColumn DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_UserDepartment
ALTER TABLE Sys_UserDepartment
    CHANGE COLUMN Creator CreateBy varchar(255) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(255) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE Sys_UserDepartment DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_WorkFlow
ALTER TABLE Sys_WorkFlow
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE Sys_WorkFlow DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_WorkFlowStep
ALTER TABLE Sys_WorkFlowStep
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE Sys_WorkFlowStep DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_WorkFlowTable
ALTER TABLE Sys_WorkFlowTable
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE Sys_WorkFlowTable DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_WorkFlowTableStep
ALTER TABLE Sys_WorkFlowTableStep
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE Sys_WorkFlowTableStep DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- Sys_WorkFlowTableAuditLog（只有 CreateDate）
ALTER TABLE Sys_WorkFlowTableAuditLog
    CHANGE COLUMN CreateDate CreateTime datetime NULL;

-- TestDb
ALTER TABLE TestDb
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE TestDb DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- TestService
ALTER TABLE TestService
    CHANGE COLUMN Creator CreateBy varchar(30) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL,
    CHANGE COLUMN Modifier UpdateBy varchar(30) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE TestService DROP COLUMN CreateID, DROP COLUMN ModifyID;

-- ========================================================================
-- 七、sys_* 系列
-- ========================================================================

-- sys_config
ALTER TABLE sys_config
    CHANGE COLUMN Creator CreateBy varchar(50) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN Modifier UpdateBy varchar(50) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL,
    CHANGE COLUMN Deleter DeleteBy varchar(50) NULL;
ALTER TABLE sys_config DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- sys_log
ALTER TABLE sys_log
    CHANGE COLUMN Creator CreateBy varchar(50) NULL,
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN Modifier UpdateBy varchar(50) NULL,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL,
    CHANGE COLUMN Deleter DeleteBy varchar(50) NULL;
ALTER TABLE sys_log DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- sys_api（只有 create_date）
ALTER TABLE sys_api
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;

-- sys_role_api
ALTER TABLE sys_role_api
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;

-- sys_user_permission
ALTER TABLE sys_user_permission
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP;

-- ========================================================================
-- 八、cert_* 部分（有 CreateDate/ModifyDate 的）
-- ========================================================================

-- 这些表已有 CreateBy/UpdateBy/DeleteBy，只需 RENAME CreateDate/ModifyDate
ALTER TABLE cert_clause_extraction_rule
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

ALTER TABLE cert_directory_template
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

ALTER TABLE cert_file_requirement
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

ALTER TABLE cert_report_template
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

ALTER TABLE cert_standard_directory_config
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE cert_standard_directory_config DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

ALTER TABLE cert_standard_directory_file
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE cert_standard_directory_file DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

ALTER TABLE cert_standard_directory_folder
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;
ALTER TABLE cert_standard_directory_folder DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

ALTER TABLE cert_standard_phase_config
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

ALTER TABLE cert_validation_rule_source
    CHANGE COLUMN CreateDate CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN ModifyDate UpdateTime datetime NULL;

-- ========================================================================
-- 校验
-- ========================================================================
SELECT '=== Remaining problematic columns ===' AS status;
SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'yzh_cert_platform'
  AND column_name IN ('Creator', 'CreateDate', 'Modifier', 'ModifyDate', 'Deleter',
                      'CreateID', 'ModifyID', 'DeleteID', 'ParentId',
                      'creator', 'create_date', 'modifier', 'modify_date', 'deleter', 'delete_time',
                      'create_by', 'update_by', 'delete_by')
ORDER BY table_name, column_name;

SET SQL_SAFE_UPDATES = 1;
