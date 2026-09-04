#!/usr/bin/env python3
"""Check Sys_Role table structure"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

# Sys_Role structure
print('=== Sys_Role columns ===')
cursor.execute('SHOW COLUMNS FROM Sys_Role')
for row in cursor.fetchall():
    print(f'  {row[0]}: {row[1]}')

print()
print('=== Sys_Role data ===')
cursor.execute('SELECT * FROM Sys_Role LIMIT 5')
cols = [desc[0] for desc in cursor.description]
print(f'  Columns: {cols}')
for row in cursor.fetchall():
    print(f'  {row}')

cursor.close()
conn.close()
