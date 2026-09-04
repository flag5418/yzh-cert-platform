#!/usr/bin/env python3
"""执行添加微信字段 SQL 脚本"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

with open('scripts/db/add_wechat_fields.sql', 'r') as f:
    sql = f.read()

for cmd in sql.split(';'):
    cmd = cmd.strip()
    if not cmd or cmd.startswith('--') or cmd.startswith('/*'):
        continue
    lines = [l for l in cmd.split('\n') if not l.strip().startswith('--')]
    final = '\n'.join(lines).strip()
    if final:
        try:
            cursor.execute(final)
            while cursor.nextset():
                pass
            print(f'OK: {final[:60]}...')
        except mysql.connector.Error as err:
            print(f'ERR: {err} | {final[:60]}...')

conn.commit()
cursor.close()
conn.close()
print('Done!')
