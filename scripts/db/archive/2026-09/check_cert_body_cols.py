#!/usr/bin/env python3
"""检查 cert_certification_body 表结构"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

print('=== cert_certification_body 列 ===')
cursor.execute('SHOW COLUMNS FROM cert_certification_body')
for r in cursor.fetchall():
    print(f'  {r[0]}: {r[1]}')

cursor.close()
conn.close()
