# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("SHOW TABLES LIKE '%prompt%'")
print('tables:', [r[0] for r in cur.fetchall()])
for t in ['wf_prompt_template','cert_prompt_template']:
    try:
        cur.execute("SHOW COLUMNS FROM `%s`" % t)
        print('===',t,[r[0] for r in cur.fetchall()])
    except Exception as e:
        print('===',t,'ERR',e)
conn.close()
