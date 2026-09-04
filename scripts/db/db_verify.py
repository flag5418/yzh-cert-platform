#!/usr/bin/env python3
"""Add role 200 + cleanup Sys_User duplicate org_code column"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

# 1. Sys_User org fields BEFORE
print('=== Sys_User org columns (BEFORE) ===')
cursor.execute("SHOW COLUMNS FROM sys_user LIKE '%org%'")
for row in cursor.fetchall():
    print(f'  {row[0]}: {row[1]}')

# 2. Drop Org_Code (duplicate) - keep OrgCode only
print()
print('=== Dropping Org_Code column (duplicate) ===')
try:
    cursor.execute("ALTER TABLE sys_user DROP COLUMN Org_Code")
    conn.commit()
    print('  Dropped Org_Code successfully')
except mysql.connector.Error as e:
    print(f'  Error: {e}')

# 3. Confirm Sys_User after
print()
print('=== Sys_User org columns (AFTER) ===')
cursor.execute("SHOW COLUMNS FROM sys_user LIKE '%org%'")
for row in cursor.fetchall():
    print(f'  {row[0]}: {row[1]}')

# 4. Insert Role 200 (no Remark column)
print()
print('=== Inserting Role 200 ===')
cursor.execute("""
INSERT INTO Sys_Role (Role_Id, RoleName, Enable, Creator, CreateDate, OrderNo, ParentId)
VALUES (200, '体系认证客户端管理员', 1, 'system', NOW(), 300, 0)
ON DUPLICATE KEY UPDATE RoleName = VALUES(RoleName)
""")
conn.commit()
print(f'  Affected: {cursor.rowcount} rows')

# 5. All Roles
print()
print('=== All Roles ===')
cursor.execute('SELECT Role_Id, RoleName, Enable FROM Sys_Role ORDER BY Role_Id')
for row in cursor.fetchall():
    print(f'  {row[0]}: {row[1]} (enable={row[2]})')

# 6. cert_certification_body list
print()
print('=== cert_certification_body (active) ===')
cursor.execute("SELECT id, code, name, short_name FROM cert_certification_body WHERE status='active' AND enable=1 ORDER BY id")
for row in cursor.fetchall():
    print(f'  id={row[0]}, code={row[1]}, name={row[2]}, short_name={row[3]}')

cursor.close()
conn.close()
print()
print('=== Done ===')
