#!/usr/bin/env python3
"""
YZH Column Audit Tool
- Scans all entity .cs files for [Column("...")] mappings
- Compares with actual database columns
- Outputs mismatches that need migration
"""

import os
import re
import mysql.connector
import json

DB_CONFIG = {
    'host': '127.0.0.1',
    'port': 3307,
    'user': 'root',
    'password': 'Yzh123456.',
    'database': 'yzh_cert_platform',
}

ENTITY_DIRS = [
    '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/VOL.Entity/Admin/Platform',
]


def get_entity_mappings(entity_file):
    """Parse entity file for [Column("...")] and [Table("...")] attributes"""
    with open(entity_file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Find table name from [Table("...")] or [Entity(TableName="...")]
    table_match = re.search(r'\[Table\("([^"]+)"\)\]', content)
    if not table_match:
        table_match = re.search(r'TableName\s*=\s*"([^"]+)"', content)
    table_name = table_match.group(1) if table_match else None

    # Find all property -> column mappings
    # Pattern: [Column("xxx")] or [Column("xxx", ...)]
    properties = {}
    lines = content.split('\n')
    current_prop = None

    for line in lines:
        # Match property declaration (public type Name { get; set; })
        prop_match = re.search(r'public\s+(?:Nullable<)?(\w+)(?:>)?\s+(\w+)\s*\{', line)
        if prop_match:
            current_prop = prop_match.group(2)
            continue

        # Match [Column("...")] 
        col_match = re.search(r'\[Column\("([^"]+)"', line)
        if col_match and current_prop:
            properties[current_prop] = col_match.group(1)
            current_prop = None  # Reset after mapping

        # Also match [Column(TypeName="...")] - this means no rename (property name = column name)
        col_type_match = re.search(r'\[Column\(TypeName=', line)
        if col_type_match and current_prop:
            # No explicit rename - EF Core uses property name
            properties[current_prop] = current_prop
            current_prop = None

    return table_name, properties


def get_db_columns(table_name):
    """Get actual column names from database"""
    conn = mysql.connector.connect(**DB_CONFIG)
    cursor = conn.cursor()
    try:
        cursor.execute(f"DESCRIBE `{table_name}`")
        columns = [row[0] for row in cursor.fetchall()]
    except Exception:
        columns = None
    finally:
        cursor.close()
        conn.close()
    return columns


def scan_entities():
    """Scan all entity files and return mappings"""
    entities = {}
    for base_dir in ENTITY_DIRS:
        if not os.path.exists(base_dir):
            continue
        for root, dirs, files in os.walk(base_dir):
            for f in files:
                if f.endswith('.cs') and not f.endswith('Designer.cs'):
                    full_path = os.path.join(root, f)
                    table_name, props = get_entity_mappings(full_path)
                    if table_name and props:
                        entities[table_name] = {
                            'file': full_path,
                            'properties': props
                        }
    return entities


def main():
    entities = scan_entities()

    print(f"Scanned {len(entities)} entities\n")
    print("=" * 80)

    results = []
    for table, info in sorted(entities.items()):
        db_columns = get_db_columns(table)
        if db_columns is None:
            print(f"TABLE NOT IN DB: {table}")
            continue

        db_set = set(db_columns)
        mismatches = []

        for prop, expected_col in info['properties'].items():
            if expected_col not in db_set:
                # Check if perhaps the PascalCase version exists
                if prop in db_set and prop != expected_col:
                    mismatches.append({
                        'property': prop,
                        'expected': expected_col,
                        'actual': prop,
                        'issue': 'needs_rename'
                    })
                elif expected_col not in db_set and prop not in db_set:
                    mismatches.append({
                        'property': prop,
                        'expected': expected_col,
                        'actual': None,
                        'issue': 'missing'
                    })
                # else: expected_col exists in DB - OK

        if mismatches:
            results.append({'table': table, 'mismatches': mismatches})
            print(f"\nTABLE: {table}")
            for m in mismatches:
                if m['issue'] == 'needs_rename':
                    print(f"   RENAME: {m['actual']} -> {m['expected']}")
                elif m['issue'] == 'missing':
                    print(f"   MISSING: {m['expected']}")

    print(f"\n{'=' * 80}")
    print(f"Total tables with issues: {len(results)}")


if __name__ == '__main__':
    main()
