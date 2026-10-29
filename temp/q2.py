# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
for t in ['cert_iso_clause','cert_clause_attr']:
    cur.execute("SHOW COLUMNS FROM `%s`" % t)
    print('===', t)
    for r in cur.fetchall():
        print(' ', r[0], r[1], r[2])
conn.close()
