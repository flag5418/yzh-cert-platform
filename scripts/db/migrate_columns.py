#!/usr/bin/env python3
"""
YZH Framework Column Migration: PascalCase → snake_case
Date: 2026-09-04
Purpose: Align database column names with YZH entity [Column("snake_case")] mappings
"""

import mysql.connector
import sys

DB_CONFIG = {
    'host': '127.0.0.1',
    'port': 3307,
    'user': 'root',
    'password': 'Yzh123456.',
    'database': 'yzh_cert_platform',
    'autocommit': False
}

# Tables and their column rename mappings
# Format: { 'table_name': {'old_name': 'new_name', ...} }
TABLES = {
    'cert_certification_body': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'cert_iso_standard': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'cert_iso_clause': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'cert_cert_stage': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'cert_application': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'cert_enterprise': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'cert_org_standard': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'cert_org_stage': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'cert_validation_rule': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'wf_workflow_definition': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'wf_execution_task': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'wf_skill_category': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'yzh_page_config': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'yzh_field_config': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'audit_task': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'audit_project': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'ent_enterprise': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
    'rpt_report_section': {
        'CreateID': 'create_id', 'Creator': 'creator', 'CreateDate': 'create_date',
        'ModifyID': 'modify_id', 'Modifier': 'modifier', 'ModifyDate': 'modify_date',
        'DeleteID': 'delete_id', 'Deleter': 'deleter', 'DeleteTime': 'delete_time',
        'Status': 'status', 'Enable': 'enable',
    },
}


def get_column_type(cursor, table, column):
    """Get the column type definition"""
    cursor.execute(f"SELECT COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='{table}' AND COLUMN_NAME='{column}'")
    row = cursor.fetchone()
    return row[0] if row else None


def main():
    conn = mysql.connector.connect(**DB_CONFIG)
    cursor = conn.cursor()
    
    total_ok = 0
    total_skip = 0
    total_err = 0
    
    for table, renames in TABLES.items():
        # Check if table exists
        cursor.execute(f"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='{table}'")
        if cursor.fetchone()[0] == 0:
            print(f" SKIP: {table} (not exists)")
            total_skip += 1
            continue
        
        # Build CHANGE COLUMN clauses
        changes = []
        for old_name, new_name in renames.items():
            # Check if old column exists
            cursor.execute(f"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='{table}' AND COLUMN_NAME='{old_name}'")
            if cursor.fetchone()[0] == 0:
                continue  # Column already renamed or doesn't exist
            
            # Check if new column already exists (from previous partial migration)
            cursor.execute(f"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='{table}' AND COLUMN_NAME='{new_name}'")
            if cursor.fetchone()[0] > 0:
                continue
            
            col_type = get_column_type(cursor, table, old_name)
            if col_type:
                changes.append(f"CHANGE COLUMN `{old_name}` `{new_name}` {col_type}")
        
        if not changes:
            print(f" SKIP: {table} (no columns to rename)")
            total_skip += 1
            continue
        
        sql = f"ALTER TABLE `{table}` " + ", ".join(changes)
        
        try:
            cursor.execute(sql)
            print(f"   OK: {table} ({len(changes)} columns renamed)")
            total_ok += 1
        except Exception as e:
            print(f"FAIL: {table}: {e}")
            total_err += 1
    
    conn.commit()
    cursor.close()
    conn.close()
    
    print(f"\n=== Migration Complete ===")
    print(f"  OK: {total_ok}, SKIP: {total_skip}, FAIL: {total_err}")


if __name__ == '__main__':
    main()
