#!/usr/bin/env python3
"""
YZH Framework Column Migration V2: Fix remaining (Creator, Modifier, Deleter, Status, Enable)
"""

import mysql.connector

DB_CONFIG = {
    'host': '127.0.0.1',
    'port': 3307,
    'user': 'root',
    'password': 'Yzh123456.',
    'database': 'yzh_cert_platform',
    'autocommit': False
}

# Get all cert_, yzh_, wf_, audit_, ent_, rpt_ tables
TABLE_PREFIXES = ('cert_', 'yzh_', 'wf_', 'audit_', 'ent_', 'rpt_')

# Column renames: old PascalCase -> new snake_case
RENAMES = {
    'Creator': 'creator',
    'Modifier': 'modifier',
    'Deleter': 'deleter',
    'Status': 'status',
    'Enable': 'enable',
}


def main():
    conn = mysql.connector.connect(**DB_CONFIG)
    cursor = conn.cursor()

    # Get all tables with YZH prefixes
    cursor.execute("""
        SELECT TABLE_NAME FROM information_schema.TABLES 
        WHERE TABLE_SCHEMA='yzh_cert_platform' 
        AND (TABLE_NAME LIKE 'cert_%%' OR TABLE_NAME LIKE 'yzh_%%' 
             OR TABLE_NAME LIKE 'wf_%%' OR TABLE_NAME LIKE 'audit_%%'
             OR TABLE_NAME LIKE 'ent_%%' OR TABLE_NAME LIKE 'rpt_%%')
    """)
    tables = [row[0] for row in cursor.fetchall()]

    total_ok = 0
    total_skip = 0
    total_err = 0

    for table in tables:
        # Get column types for this table
        cursor.execute("""
            SELECT COLUMN_NAME, COLUMN_TYPE FROM information_schema.COLUMNS 
            WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME=%s
        """, (table,))
        columns = {row[0]: row[1] for row in cursor.fetchall()}

        changes = []
        for old_name, new_name in RENAMES.items():
            if old_name in columns and new_name not in columns:
                col_type = columns[old_name]
                changes.append(f"CHANGE COLUMN `{old_name}` `{new_name}` {col_type}")

        if not changes:
            total_skip += 1
            continue

        sql = f"ALTER TABLE `{table}` " + ", ".join(changes)

        try:
            cursor.execute(sql)
            print(f"   OK: {table} ({len(changes)} columns)")
            total_ok += 1
        except Exception as e:
            print(f"FAIL: {table}: {e}")
            total_err += 1

    conn.commit()
    cursor.close()
    conn.close()

    print(f"\n=== Migration V2 Complete ===")
    print(f"  OK: {total_ok}, SKIP: {total_skip}, FAIL: {total_err}")


if __name__ == '__main__':
    main()
