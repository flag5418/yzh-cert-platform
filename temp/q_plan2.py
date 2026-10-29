# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("SHOW INDEX FROM cert_audit_plan")
print('=== cert_audit_plan 索引')
for r in cur.fetchall(): print(f'   {r[2]:<28} col={r[4]:<20} seq={r[3]} nonuniq={r[1]}')
cur.execute("SHOW INDEX FROM cert_audit_plan_detail")
print('=== cert_audit_plan_detail 索引')
for r in cur.fetchall(): print(f'   {r[2]:<28} col={r[4]:<20} seq={r[3]} nonuniq={r[1]}')
cur.execute("SHOW COLUMNS FROM cert_audit_plan_detail")
print('=== detail 列:', ', '.join(r[0] for r in cur.fetchall()))
conn.close()
