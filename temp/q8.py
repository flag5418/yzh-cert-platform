# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("SHOW INDEX FROM cert_prompt_template")
for r in cur.fetchall(): print(r[2], r[4], 'nonunique=',r[1])
print('--- TempAttr 行')
cur.execute("SELECT Id,Code,AttrName,IsDeleted FROM cert_clause_attr_def WHERE Code='TempAttr'")
print(cur.fetchall())
conn.close()
