# -*- coding: utf-8 -*-
import json, pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("""SELECT c.ClauseNumber, c.Code, a.AttrValue FROM cert_clause_attr a
                 JOIN cert_iso_clause c ON c.Code=a.ClauseCode
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0 AND a.AttrCode='Process'""")
for num, code, v in cur.fetchall():
    try:
        n = len(json.loads(v or '[]'))
    except Exception:
        n = -1
    if n == 0 or n > 3:
        print(num, code, v)
conn.close()
