# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("SHOW COLUMNS FROM Sys_Menu")
print([r[0] for r in cur.fetchall()])
cur.execute("SELECT Code,MenuName,Url,ParentCode FROM Sys_Menu WHERE Url LIKE '%resource%' OR Url LIKE '%original%' OR Url LIKE '%audit-plan%'")
for r in cur.fetchall(): print(' ', r)
conn.close()
