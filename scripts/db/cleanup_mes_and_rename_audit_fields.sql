-- ====================================================================
-- 数据库清理脚本 V1
-- 1. 删除所有 MES 开头的表（35张）
-- 2. 审计字段重命名并改为 varchar(50) 存储用户 Code
--    迁移策略：原 int ID → 通过 Sys_User 关联获取 Code
-- ====================================================================

SET FOREIGN_KEY_CHECKS = 0;
SET SESSION sql_mode = 'NO_AUTO_VALUE_ON_ZERO';

-- ====================================================================
-- 第一部分：删除所有 MES 开头的表
-- ====================================================================
DROP TABLE IF EXISTS MES_Bom_Detail;
DROP TABLE IF EXISTS MES_Bom_Main;
DROP TABLE IF EXISTS MES_Customer;
DROP TABLE IF EXISTS MES_DefectiveProductDisposalRecord;
DROP TABLE IF EXISTS MES_DefectiveProductRecord;
DROP TABLE IF EXISTS MES_EquipmentFaultRecord;
DROP TABLE IF EXISTS MES_EquipmentMaintenance;
DROP TABLE IF EXISTS MES_EquipmentManagement;
DROP TABLE IF EXISTS MES_EquipmentRepair;
DROP TABLE IF EXISTS MES_InventoryManagement;
DROP TABLE IF EXISTS MES_LocationManagement;
DROP TABLE IF EXISTS MES_Material;
DROP TABLE IF EXISTS MES_MaterialCatalog;
DROP TABLE IF EXISTS MES_Process;
DROP TABLE IF EXISTS MES_ProcessReport;
DROP TABLE IF EXISTS MES_ProcessRoute;
DROP TABLE IF EXISTS MES_ProductInbound;
DROP TABLE IF EXISTS MES_ProductionLine;
DROP TABLE IF EXISTS MES_ProductionLineDevice;
DROP TABLE IF EXISTS MES_ProductionOrder;
DROP TABLE IF EXISTS MES_ProductionPlanChangeRecord;
DROP TABLE IF EXISTS MES_ProductionPlanDetail;
DROP TABLE IF EXISTS MES_ProductionReporting;
DROP TABLE IF EXISTS MES_ProductionReportingDetail;
DROP TABLE IF EXISTS MES_ProductOutbound;
DROP TABLE IF EXISTS MES_QualityInspectionPlan;
DROP TABLE IF EXISTS MES_QualityInspectionPlanDetail;
DROP TABLE IF EXISTS MES_QualityInspectionRecord;
DROP TABLE IF EXISTS MES_SchedulingPlan;
DROP TABLE IF EXISTS MES_Supplier;
DROP TABLE IF EXISTS MES_WarehouseManagement;

-- ====================================================================
-- 第二部分：审计字段迁移函数
-- 迁移步骤：
--   1. 添加临时字段 _tmp (varchar)
--   2. UPDATE JOIN Sys_User 填充 Code
--   3. 删除原字段
--   4. 重命名临时字段
-- ====================================================================

-- 由于 MySQL 不支持在事务中执行 DDL，我们使用以下策略：
-- 方案：直接 CHANGE COLUMN 类型 + 名称，然后用 UPDATE 填充数据
-- 注意：这会丢失原 int ID 的关联，但用户要求全部改为 Code

-- 先创建一个临时映射表来加速迁移
CREATE TEMPORARY TABLE IF NOT EXISTS _tmp_user_id_to_code (
    user_id INT PRIMARY KEY,
    user_code VARCHAR(50)
) ENGINE=MEMORY;

INSERT INTO _tmp_user_id_to_code (user_id, user_code)
SELECT User_Id, COALESCE(Code, CONCAT('USER_', LPAD(User_Id, 6, '0')))
FROM Sys_User;

-- ====================================================================
-- 第三部分：audit_* 系列表（PascalCase命名）
-- ====================================================================

-- audit_checklist_item
ALTER TABLE audit_checklist_item 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE audit_checklist_item t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE audit_checklist_item t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE audit_checklist_item t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE audit_checklist_item DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- audit_evidence
ALTER TABLE audit_evidence 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE audit_evidence t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE audit_evidence t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE audit_evidence t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE audit_evidence DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- audit_finding
ALTER TABLE audit_finding 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE audit_finding t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE audit_finding t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE audit_finding t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE audit_finding DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- audit_nonconformity
ALTER TABLE audit_nonconformity 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE audit_nonconformity t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE audit_nonconformity t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE audit_nonconformity t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE audit_nonconformity DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- audit_rectification
ALTER TABLE audit_rectification 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE audit_rectification t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE audit_rectification t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE audit_rectification t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE audit_rectification DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ====================================================================
-- 第四部分：cert_* 系列表（snake_case命名）
-- ====================================================================

-- cert_ai_config (特殊：有 create_id + update_id + modify_id + delete_id)
-- 先处理 modify_id → update_by，然后处理 update_id → update_by（合并）
ALTER TABLE cert_ai_config 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_ai_config t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
-- 优先使用 modify_id，其次 update_id
UPDATE cert_ai_config t
  LEFT JOIN _tmp_user_id_to_code u ON COALESCE(t.modify_id, t.update_id) = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(COALESCE(t.modify_id, t.update_id), 6, '0')));
UPDATE cert_ai_config t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_ai_config DROP COLUMN create_id, DROP COLUMN update_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_application
ALTER TABLE cert_application 
  ADD COLUMN create_by VARCHAR(50) AFTER Remark,
  ADD COLUMN update_by VARCHAR(50) AFTER create_by;
UPDATE cert_application t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_application t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
ALTER TABLE cert_application DROP COLUMN create_id, DROP COLUMN modify_id;

-- cert_auditor_profile
ALTER TABLE cert_auditor_profile 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_auditor_profile t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_auditor_profile t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_auditor_profile t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_auditor_profile DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_cert_stage
ALTER TABLE cert_cert_stage 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_cert_stage t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_cert_stage t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_cert_stage t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_cert_stage DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_certification_body
ALTER TABLE cert_certification_body 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_certification_body t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_certification_body t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_certification_body t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_certification_body DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_clause_extraction_rule
ALTER TABLE cert_clause_extraction_rule 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_clause_extraction_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_clause_extraction_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_clause_extraction_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_clause_extraction_rule DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_directory_template
ALTER TABLE cert_directory_template 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_directory_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_directory_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_directory_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_directory_template DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_doc_extraction_rule
ALTER TABLE cert_doc_extraction_rule 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_doc_extraction_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_doc_extraction_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_doc_extraction_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_doc_extraction_rule DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_doc_field_def
ALTER TABLE cert_doc_field_def 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_doc_field_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_doc_field_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_doc_field_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_doc_field_def DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_doc_table_def
ALTER TABLE cert_doc_table_def 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_doc_table_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_doc_table_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_doc_table_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_doc_table_def DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_doc_table_field_def
ALTER TABLE cert_doc_table_field_def 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_doc_table_field_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_doc_table_field_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_doc_table_field_def t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_doc_table_field_def DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_enterprise
ALTER TABLE cert_enterprise 
  ADD COLUMN create_by VARCHAR(50) AFTER Remark,
  ADD COLUMN update_by VARCHAR(50) AFTER create_by;
UPDATE cert_enterprise t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_enterprise t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
ALTER TABLE cert_enterprise DROP COLUMN create_id, DROP COLUMN modify_id;

-- cert_file_requirement
ALTER TABLE cert_file_requirement 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_file_requirement t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_file_requirement t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_file_requirement t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_file_requirement DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_iso_clause
ALTER TABLE cert_iso_clause 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_iso_clause t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_iso_clause t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_iso_clause t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_iso_clause DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_iso_standard
ALTER TABLE cert_iso_standard 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_iso_standard t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_iso_standard t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_iso_standard t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_iso_standard DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_org_stage
ALTER TABLE cert_org_stage 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_org_stage t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_org_stage t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_org_stage t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_org_stage DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_org_standard
ALTER TABLE cert_org_standard 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_org_standard t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_org_standard t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_org_standard t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_org_standard DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_phase_definition
ALTER TABLE cert_phase_definition 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_phase_definition t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_phase_definition t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_phase_definition t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_phase_definition DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_report_template
ALTER TABLE cert_report_template 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_report_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_report_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_report_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_report_template DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_standard_directory_config
ALTER TABLE cert_standard_directory_config 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_standard_directory_config t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_standard_directory_config t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_standard_directory_config t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_standard_directory_config DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_standard_directory_file
ALTER TABLE cert_standard_directory_file 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_standard_directory_file t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_standard_directory_file t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_standard_directory_file t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_standard_directory_file DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_standard_directory_folder
ALTER TABLE cert_standard_directory_folder 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_standard_directory_folder t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_standard_directory_folder t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_standard_directory_folder t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_standard_directory_folder DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_standard_phase_config
ALTER TABLE cert_standard_phase_config 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_standard_phase_config t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_standard_phase_config t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_standard_phase_config t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_standard_phase_config DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- cert_validation_rule
ALTER TABLE cert_validation_rule 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE cert_validation_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE cert_validation_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE cert_validation_rule t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE cert_validation_rule DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- cert_validation_rule_source
ALTER TABLE cert_validation_rule_source 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE cert_validation_rule_source t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE cert_validation_rule_source t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE cert_validation_rule_source t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE cert_validation_rule_source DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ====================================================================
-- 第五部分：ent_* 系列表
-- ====================================================================

-- ent_enterprise
ALTER TABLE ent_enterprise 
  ADD COLUMN create_by VARCHAR(50) AFTER Remark,
  ADD COLUMN update_by VARCHAR(50) AFTER create_by,
  ADD COLUMN delete_by VARCHAR(50) AFTER delete_time;
UPDATE ent_enterprise t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE ent_enterprise t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE ent_enterprise t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE ent_enterprise DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ent_enterprise_document
ALTER TABLE ent_enterprise_document 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE ent_enterprise_document t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE ent_enterprise_document t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE ent_enterprise_document t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE ent_enterprise_document DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ent_enterprise_file
ALTER TABLE ent_enterprise_file 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE ent_enterprise_file t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE ent_enterprise_file t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE ent_enterprise_file t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE ent_enterprise_file DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ent_enterprise_phase
ALTER TABLE ent_enterprise_phase 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE ent_enterprise_phase t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE ent_enterprise_phase t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE ent_enterprise_phase t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE ent_enterprise_phase DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ent_extraction_result
ALTER TABLE ent_extraction_result 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE ent_extraction_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE ent_extraction_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE ent_extraction_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE ent_extraction_result DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ent_file_compliance_check
ALTER TABLE ent_file_compliance_check 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE ent_file_compliance_check t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE ent_file_compliance_check t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE ent_file_compliance_check t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE ent_file_compliance_check DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ent_file_pre_check_result
ALTER TABLE ent_file_pre_check_result 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE ent_file_pre_check_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE ent_file_pre_check_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE ent_file_pre_check_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE ent_file_pre_check_result DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ent_file_version
ALTER TABLE ent_file_version 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE ent_file_version t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE ent_file_version t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE ent_file_version t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE ent_file_version DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ent_table_extraction_result
ALTER TABLE ent_table_extraction_result 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE ent_table_extraction_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE ent_table_extraction_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE ent_table_extraction_result t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE ent_table_extraction_result DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ====================================================================
-- 第六部分：rpt_* 系列表
-- ====================================================================

-- rpt_audit_report
ALTER TABLE rpt_audit_report 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE rpt_audit_report t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE rpt_audit_report t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE rpt_audit_report t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE rpt_audit_report DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- rpt_report_section
ALTER TABLE rpt_report_section 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE rpt_report_section t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE rpt_report_section t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE rpt_report_section t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE rpt_report_section DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- rpt_report_section_source
ALTER TABLE rpt_report_section_source 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE rpt_report_section_source t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE rpt_report_section_source t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE rpt_report_section_source t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE rpt_report_section_source DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- rpt_report_task
ALTER TABLE rpt_report_task 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE rpt_report_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE rpt_report_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE rpt_report_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE rpt_report_task DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ====================================================================
-- 第七部分：wf_* 系列表
-- ====================================================================

-- wf_execution_task
ALTER TABLE wf_execution_task 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_execution_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_execution_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_execution_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_execution_task DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- wf_execution_task_item
ALTER TABLE wf_execution_task_item 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_execution_task_item t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
</longcat_think>
