# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("SELECT Code,StageName,StageCode FROM cert_cert_stage WHERE IsDeleted=0")
print('=== cert_cert_stage')
for r in cur.fetchall(): print(' ', r)
print()
cur.execute("SELECT Code,Name FROM cert_enterprise WHERE IsDeleted=0")
print('=== 企业')
for r in cur.fetchall(): print(' ', r)
print()
cur.execute("SELECT * FROM cert_enterprise_stage")
print('=== cert_enterprise_stage 全部')
cur.execute("SHOW COLUMNS FROM cert_enterprise_stage")
cols=[r[0] for r in cur.fetchall()]
cur.execute("SELECT * FROM cert_enterprise_stage")
for r in cur.fetchall():
    print(' ', dict(zip(cols,r)))
conn.close()
