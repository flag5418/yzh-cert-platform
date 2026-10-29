# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("SELECT UserName,IsValid,IsDeleted FROM Sys_User LIMIT 10")
for r in cur.fetchall(): print(r)
conn.close()
