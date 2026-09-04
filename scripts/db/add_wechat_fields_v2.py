#!/usr/bin/env python3
"""添加微信字段到 Sys_User"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

statements = [
    "ALTER TABLE sys_user ADD COLUMN wechat_openid VARCHAR(64) DEFAULT NULL COMMENT '微信 OpenID'",
    "ALTER TABLE sys_user ADD COLUMN wechat_unionid VARCHAR(64) DEFAULT NULL COMMENT '微信 UnionID'",
    "ALTER TABLE sys_user ADD UNIQUE INDEX uk_wechat_openid (wechat_openid)",
    "ALTER TABLE sys_user ADD UNIQUE INDEX uk_wechat_unionid (wechat_unionid)",
]

for sql in statements:
    try:
        cursor.execute(sql)
        conn.commit()
        print(f'OK: {sql[:60]}...')
    except mysql.connector.Error as err:
        print(f'ERR: {err} | {sql[:60]}...')

# 验证
print()
print('=== 验证 ===')
cursor.execute("SHOW COLUMNS FROM sys_user WHERE Field LIKE '%wechat%'")
for r in cursor.fetchall():
    print(f'  {r[0]}: {r[1]}')

cursor.close()
conn.close()
print('Done!')
