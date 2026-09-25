#!/usr/bin/env python3
"""查询 sys_menu 表结构"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

print('=== sys_menu 列 ===')
cursor.execute('SHOW COLUMNS FROM sys_menu')
for r in cursor.fetchall():
    print(f'  {r[0]}: {r[1]}')

print()
print('=== sys_menu 数据 ===')
cursor.execute('SELECT * FROM sys_menu ORDER BY SortOrder')
rows = cursor.fetchall()

# Get column names
cursor.execute('SHOW COLUMNS FROM sys_menu')
cols = [r[0] for r in cursor.fetchall()]
print(f'Columns: {cols}')
print()

for row in rows:
    data = dict(zip(col, row) for col, row in [(c, row[cols.index(c)]) for c in cols])
    print(f"  Id={data.get('Id')}, MenuName={data.get('MenuName')}, Url={data.get('Url')}, ParentId={data.get('ParentId')}, Enable={data.get('Enable')}")

cursor.close()
conn.close()
