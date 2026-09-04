#!/usr/bin/env python3
"""检查 Sys_User 微信字段"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
c = conn.cursor()

print('=== 所有列 ===')
c.execute('SHOW COLUMNS FROM sys_user')
for r in c.fetchall():
    print(f'  {r[0]}: {r[1]}')

print()
print('=== 微信相关 ===')
c.execute("SHOW COLUMNS FROM sys_user WHERE Field LIKE '%wechat%' OR Field LIKE '%openid%' OR Field LIKE '%unionid%'")
for r in c.fetchall():
    print(f'  {r[0]}: {r[1]}')

c.close()
conn.close()
