-- ====================================================================
-- 数据库清理脚本 V1 - Part 2（续）
-- wf_* 系列 + yzh_* 系列 + audit_task + audit_project
-- ====================================================================

SET FOREIGN_KEY_CHECKS = 0;

-- 临时映射表（如果不存在则创建）
CREATE TEMPORARY TABLE IF NOT EXISTS _tmp_user_id_to_code (
    user_id INT PRIMARY KEY,
    user_code VARCHAR(50)
) ENGINE=MEMORY;

INSERT IGNORE INTO _tmp_user_id_to_code (user_id, user_code)
SELECT User_Id, COALESCE(Code, CONCAT('USER_', LPAD(User_Id, 6, '0')))
FROM Sys_User;

-- ====================================================================
-- audit_task (snake_case命名)
-- ====================================================================
ALTER TABLE audit_task 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE audit_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE audit_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE audit_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE audit_task DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- audit_project (snake_case命名)
-- ====================================================================
ALTER TABLE audit_project 
  ADD COLUMN create_by VARCHAR(50) AFTER Remark,
  ADD COLUMN update_by VARCHAR(50) AFTER create_by;
UPDATE audit_project t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE audit_project t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
ALTER TABLE audit_project DROP COLUMN create_id, DROP COLUMN modify_id;

-- ====================================================================
-- wf_field_label_mapping
-- ====================================================================
ALTER TABLE wf_field_label_mapping 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE wf_field_label_mapping t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE wf_field_label_mapping t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE wf_field_label_mapping t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE wf_field_label_mapping DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ====================================================================
-- wf_node_execution
-- ====================================================================
ALTER TABLE wf_node_execution 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_node_execution t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_node_execution t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_node_execution t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_node_execution DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- wf_prompt_template
-- ====================================================================
ALTER TABLE wf_prompt_template 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_prompt_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_prompt_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_prompt_template t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_prompt_template DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- wf_skill_api
-- ====================================================================
ALTER TABLE wf_skill_api 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_skill_api t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_skill_api t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_skill_api t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_skill_api DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- wf_skill_category
-- ====================================================================
ALTER TABLE wf_skill_category 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_skill_category t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_skill_category t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_skill_category t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_skill_category DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- wf_skill_input
-- ====================================================================
ALTER TABLE wf_skill_input 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_skill_input t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_skill_input t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_skill_input t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_skill_input DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- wf_skill_output
-- ====================================================================
ALTER TABLE wf_skill_output 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_skill_output t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_skill_output t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_skill_output t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_skill_output DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- wf_skill_reflection
-- ====================================================================
ALTER TABLE wf_skill_reflection 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_skill_reflection t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_skill_reflection t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_skill_reflection t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_skill_reflection DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- wf_workflow_definition
-- ====================================================================
ALTER TABLE wf_workflow_definition 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE wf_workflow_definition t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE wf_workflow_definition t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE wf_workflow_definition t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE wf_workflow_definition DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- wf_workflow_execution_log
-- ====================================================================
ALTER TABLE wf_workflow_execution_log 
  ADD COLUMN CreateBy VARCHAR(50) AFTER creator,
  ADD COLUMN UpdateBy VARCHAR(50) AFTER modifier,
  ADD COLUMN DeleteBy VARCHAR(50) AFTER deleter;
UPDATE wf_workflow_execution_log t
  LEFT JOIN _tmp_user_id_to_code u ON t.CreateID = u.user_id
  SET t.CreateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.CreateID, 6, '0')));
UPDATE wf_workflow_execution_log t
  LEFT JOIN _tmp_user_id_to_code u ON t.ModifyID = u.user_id
  SET t.UpdateBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.ModifyID, 6, '0')));
UPDATE wf_workflow_execution_log t
  LEFT JOIN _tmp_user_id_to_code u ON t.DeleteID = u.user_id
  SET t.DeleteBy = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.DeleteID, 6, '0')));
ALTER TABLE wf_workflow_execution_log DROP COLUMN CreateID, DROP COLUMN ModifyID, DROP COLUMN DeleteID;

-- ====================================================================
-- yzh_* 系列表
-- ====================================================================

-- yzh_queue
ALTER TABLE yzh_queue 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE yzh_queue t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE yzh_queue t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE yzh_queue t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE yzh_queue DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- yzh_queue_resource_lock
ALTER TABLE yzh_queue_resource_lock 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE yzh_queue_resource_lock t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE yzh_queue_resource_lock t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE yzh_queue_resource_lock t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE yzh_queue_resource_lock DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- yzh_queue_task
ALTER TABLE yzh_queue_task 
  ADD COLUMN create_by VARCHAR(50) AFTER creator,
  ADD COLUMN update_by VARCHAR(50) AFTER modifier,
  ADD COLUMN delete_by VARCHAR(50) AFTER deleter;
UPDATE yzh_queue_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.create_id = u.user_id
  SET t.create_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.create_id, 6, '0')));
UPDATE yzh_queue_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.modify_id = u.user_id
  SET t.update_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.modify_id, 6, '0')));
UPDATE yzh_queue_task t
  LEFT JOIN _tmp_user_id_to_code u ON t.delete_id = u.user_id
  SET t.delete_by = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.delete_id, 6, '0')));
ALTER TABLE yzh_queue_task DROP COLUMN create_id, DROP COLUMN modify_id, DROP COLUMN delete_id;

-- ====================================================================
-- 清理临时表
-- ====================================================================
DROP TEMPORARY TABLE IF EXISTS _tmp_user_id_to_code;

-- 恢复外键检查
SET FOREIGN_KEY_CHECKS = 1;

-- ====================================================================
-- 验证：检查是否还有 CreateID/ModifyID/DeleteID 字段残留
-- ====================================================================
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE 
FROM information_schema.COLUMNS 
WHERE TABLE_SCHEMA='yzh_cert_platform' 
  AND (COLUMN_NAME LIKE '%CreateID%' OR COLUMN_NAME LIKE '%ModifyID%' OR COLUMN_NAME LIKE '%DeleteID%'
       OR COLUMN_NAME LIKE '%create_id%' OR COLUMN_NAME LIKE '%modify_id%' OR COLUMN_NAME LIKE '%delete_id%')
ORDER BY TABLE_NAME, COLUMN_NAME;
