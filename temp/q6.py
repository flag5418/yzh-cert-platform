# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("SELECT Id,PromptCode,Template FROM cert_prompt_template WHERE IsDeleted=0")
for r in cur.fetchall():
    print('=====', r[0], r[1])
    print(r[2])
conn.close()
