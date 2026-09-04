#!/usr/bin/env python3
"""Verify newly registered user has Role_Id=200"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

print('=== Newly created user testuser_1788485555 ===')
cursor.execute("""
SELECT u.User_Id, u.UserName, u.UserTrueName, u.Role_Id, r.RoleName, u.OrgId, u.OrgCode, u.Enable
FROM sys_user u
LEFT JOIN Sys_Role r ON u.Role_Id = r.Role_Id
WHERE u.UserName = 'testuser_1788485555'
""")
row = cursor.fetchone()
if row:
    print(f'  User_Id: {row[0]}')
    print(f'  UserName: {row[1]}')
    print(f'  UserTrueName: {row[2]}')
    print(f'  Role_Id: {row[3]}')
    print(f'  RoleName: {row[4]}')
    print(f'  OrgId: {row[5]}')
    print(f'  OrgCode: {row[6]}')
    print(f'  Enable: {row[7]}')
else:
    print('  User not found!')

print()
print('=== All Roles (confirm 200) ===')
cursor.execute('SELECT Role_Id, RoleName FROM Sys_Role WHERE Role_Id IN (20, 200, 201, 202) ORDER BY Role_Id')
for row in cursor.fetchall():
    print(f'  {row[0]}: {row[1]}')

cursor.close()
conn.close()
