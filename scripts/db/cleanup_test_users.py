#!/usr/bin/env python3
"""清理测试用户"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

# 删除测试用户
cursor.execute("DELETE FROM sys_user WHERE UserName LIKE 'simple_%' OR UserName LIKE 'test_%'")
conn.commit()
print(f'Deleted {cursor.rowcount} test users')

# 验证
cursor.execute("SELECT COUNT(*) FROM sys_user WHERE UserName LIKE 'simple_%'")
print(f'Remaining test users: {cursor.fetchone()[0]}')

cursor.close()
conn.close()
print('Cleanup done')
