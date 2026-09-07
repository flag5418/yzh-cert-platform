#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
审计字段迁移脚本
将 CreateID/ModifyID/DeleteID (int) → CreateBy/UpdateBy/DeleteBy (varchar 存储用户 Code)
迁移策略：通过 Sys_User 表关联获取 User_Id 对应的 Code
"""

import subprocess
import sys

DB_CMD = ["docker", "exec", "yzh-mysql", "mysql", "-uroot", "-pYzh123456.", "yzh_cert_platform"]

def run_sql(sql, description=""):
    """执行 SQL 语句"""
    if description:
        print(f"[执行] {description}")
    try:
        result = subprocess.run(
            DB_CMD + ["-e", sql],
            capture_output=True,
            text=True,
            timeout=60
        )
        if result.returncode != 0:
            print(f"[错误] {result.stderr}")
            return False
        if result.stdout and "WARNING" not in result.stdout:
            print(f"[结果] {result.stdout.strip()[:200]}")
        return True
    except Exception as e:
        print(f"[异常] {e}")
        return False

# 需要处理的表及其审计字段配置
# 格式：(table_name, has_create, has_modify, has_delete, naming_style)
# naming_style: 'pascal' 或 'snake'
TABLES_CONFIG = [
    # audit_* 系列 (PascalCase)
    ("audit_checklist_item", True, True, True, "pascal"),
    ("audit_evidence", True, True, True, "pascal"),
    ("audit_finding", True, True, True, "pascal"),
    ("audit_nonconformity", True, True, True, "pascal"),
    ("audit_rectification", True, True, True, "pascal"),
    # audit_* 系列 (snake_case)
    ("audit_task", True, True, True, "snake"),
    ("audit_project", True, True, False, "snake"),
    # cert_* 系列 (snake_case)
    ("cert_ai_config", True, True, True, "snake"),
    ("cert_application", True, True, False, "snake"),
    ("cert_auditor_profile", True, True, True, "snake"),
    ("cert_cert_stage", True, True, True, "snake"),
    ("cert_certification_body", True, True, True, "snake"),
    ("cert_iso_clause", True, True, True, "snake"),
    ("cert_iso_standard", True, True, True, "snake"),
    ("cert_org_stage", True, True, True, "snake"),
    ("cert_org_standard", True, True, True, "snake"),
    ("cert_enterprise", True, True, False, "snake"),
    ("cert_validation_rule", True, True, True, "snake"),
    # cert_* 系列 (PascalCase)
    ("cert_clause_extraction_rule", True, True, True, "pascal"),
    ("cert_directory_template", True, True, True, "pascal"),
    ("cert_file_requirement", True, True, True, "pascal"),
    ("cert_phase_definition", True, True, True, "pascal"),
    ("cert_report_template", True, True, True, "pascal"),
    ("cert_standard_directory_config", True, True, True, "pascal"),
    ("cert_standard_directory_file", True, True, True, "pascal"),
    ("cert_standard_directory_folder", True, True, True, "pascal"),
    ("cert_standard_phase_config", True, True, True, "pascal"),
    ("cert_validation_rule_source", True, True, True, "pascal"),
    # cert_doc_* 系列 (snake_case)
    ("cert_doc_extraction_rule", True, True, True, "snake"),
    ("cert_doc_field_def", True, True, True, "snake"),
    ("cert_doc_table_def", True, True, True, "snake"),
    ("cert_doc_table_field_def", True, True, True, "snake"),
    # ent_* 系列 (snake_case + PascalCase)
    ("ent_enterprise", True, True, True, "snake"),
    ("ent_enterprise_document", True, True, True, "pascal"),
    ("ent_enterprise_file", True, True, True, "pascal"),
    ("ent_enterprise_phase", True, True, True, "pascal"),
    ("ent_extraction_result", True, True, True, "pascal"),
    ("ent_file_compliance_check", True, True, True, "pascal"),
    ("ent_file_pre_check_result", True, True, True, "pascal"),
    ("ent_file_version", True, True, True, "pascal"),
    ("ent_table_extraction_result", True, True, True, "pascal"),
    # rpt_* 系列
    ("rpt_audit_report", True, True, True, "pascal"),
    ("rpt_report_section", True, True, True, "snake"),
    ("rpt_report_section_source", True, True, True, "pascal"),
    ("rpt_report_task", True, True, True, "pascal"),
    # wf_* 系列 (snake_case)
    ("wf_execution_task", True, True, True, "snake"),
    ("wf_execution_task_item", True, True, True, "snake"),
    ("wf_node_execution", True, True, True, "snake"),
    ("wf_prompt_template", True, True, True, "snake"),
    ("wf_skill_api", True, True, True, "snake"),
    ("wf_skill_category", True, True, True, "snake"),
    ("wf_skill_input", True, True, True, "snake"),
    ("wf_skill_output", True, True, True, "snake"),
    ("wf_skill_reflection", True, True, True, "snake"),
    ("wf_workflow_definition", True, True, True, "snake"),
    # wf_* 系列 (PascalCase)
    ("wf_field_label_mapping", True, True, True, "pascal"),
    ("wf_workflow_execution_log", True, True, True, "pascal"),
    # yzh_* 系列
    ("yzh_queue", True, True, True, "snake"),
    ("yzh_queue_resource_lock", True, True, True, "snake"),
    ("yzh_queue_task", True, True, True, "snake"),
]

def migrate_table(table_name, has_create, has_modify, has_delete, naming_style):
    """迁移单个表的审计字段"""
    # 确定字段命名
    if naming_style == "pascal":
        create_col = "CreateID"
        modify_col = "ModifyID"
        delete_col = "DeleteID"
        new_create = "CreateBy"
        new_modify = "UpdateBy"
        new_delete = "DeleteBy"
    else:
        create_col = "create_id"
        modify_col = "modify_id"
        delete_col = "delete_id"
        new_create = "create_by"
        new_modify = "update_by"
        new_delete = "delete_by"
    
    # 检查字段是否存在
    check_sql = f"SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='{table_name}' AND COLUMN_NAME='{create_col}'"
    result = subprocess.run(DB_CMD + ["-N", "-e", check_sql], capture_output=True, text=True)
    if not result.stdout.strip():
        print(f"[跳过] {table_name}: 字段 {create_col} 不存在")
        return True
    
    # 构建迁移 SQL
    sql_statements = []
    
    # 1. 添加新字段
    if has_create:
        sql_statements.append(f"ALTER TABLE {table_name} ADD COLUMN {new_create} VARCHAR(50)")
    if has_modify:
        sql_statements.append(f"ALTER TABLE {table_name} ADD COLUMN {new_modify} VARCHAR(50)")
    if has_delete:
        sql_statements.append(f"ALTER TABLE {table_name} ADD COLUMN {new_delete} VARCHAR(50)")
    
    # 2. 更新数据 - 通过 Sys_User 关联
    if has_create:
        sql_statements.append(f"""
            UPDATE {table_name} t
            LEFT JOIN (SELECT User_Id, COALESCE(Code, CONCAT('USER_', LPAD(User_Id, 6, '0'))) as user_code FROM Sys_User) u 
            ON t.{create_col} = u.User_Id
            SET t.{new_create} = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.{create_col}, 6, '0')))
            WHERE t.{create_col} IS NOT NULL AND t.{create_col} > 0
        """)
    
    if has_modify:
        sql_statements.append(f"""
            UPDATE {table_name} t
            LEFT JOIN (SELECT User_Id, COALESCE(Code, CONCAT('USER_', LPAD(User_Id, 6, '0'))) as user_code FROM Sys_User) u 
            ON t.{modify_col} = u.User_Id
            SET t.{new_modify} = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.{modify_col}, 6, '0')))
            WHERE t.{modify_col} IS NOT NULL AND t.{modify_col} > 0
        """)
    
    if has_delete:
        sql_statements.append(f"""
            UPDATE {table_name} t
            LEFT JOIN (SELECT User_Id, COALESCE(Code, CONCAT('USER_', LPAD(User_Id, 6, '0'))) as user_code FROM Sys_User) u 
            ON t.{delete_col} = u.User_Id
            SET t.{new_delete} = COALESCE(u.user_code, CONCAT('USER_', LPAD(t.{delete_col}, 6, '0')))
            WHERE t.{delete_col} IS NOT NULL AND t.{delete_col} > 0
        """)
    
    # 3. 删除旧字段
    drop_cols = []
    if has_create:
        drop_cols.append(create_col)
    if has_modify:
        drop_cols.append(modify_col)
    if has_delete:
        drop_cols.append(delete_col)
    sql_statements.append(f"ALTER TABLE {table_name} DROP COLUMN {', DROP COLUMN '.join(drop_cols)}")
    
    # 执行所有 SQL
    full_sql = ";\n".join(sql_statements)
    return run_sql(full_sql, f"迁移 {table_name}")

def main():
    print("=" * 60)
    print("开始审计字段迁移: *ID (int) → *By (varchar, 用户Code)")
    print("=" * 60)
    
    success = 0
    failed = 0
    
    for table_name, has_create, has_modify, has_delete, naming_style in TABLES_CONFIG:
        if migrate_table(table_name, has_create, has_modify, has_delete, naming_style):
            success += 1
        else:
            failed += 1
    
    print("=" * 60)
    print(f"迁移完成: 成功 {success}, 失败 {failed}")
    print("=" * 60)
    
    # 验证：检查是否还有旧字段
    print("\n[验证] 检查残留字段...")
    verify_sql = """
        SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE 
        FROM information_schema.COLUMNS 
        WHERE TABLE_SCHEMA='yzh_cert_platform' 
          AND (COLUMN_NAME IN ('CreateID','ModifyID','DeleteID','create_id','modify_id','delete_id',
                               'CreatorID','ModifierID','DeleterID','creator_id','modifier_id','deleter_id'))
        ORDER BY TABLE_NAME, COLUMN_NAME
    """
    subprocess.run(DB_CMD + ["-e", verify_sql])

if __name__ == "__main__":
    main()
