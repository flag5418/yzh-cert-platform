# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("""SELECT c.ClauseNumber, c.Title, a.AttrValue, s2.AttrValue
                 FROM cert_clause_attr a
                 JOIN cert_iso_clause c ON c.Code=a.ClauseCode
                 LEFT JOIN cert_clause_attr s2 ON s2.ClauseCode=a.ClauseCode
                      AND s2.AttrCode='Severity' AND s2.IsDeleted=0
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0 AND a.AttrCode='DocCheckMode'
                ORDER BY c.Sort""")
for r in cur.fetchall():
    print(f'{r[0]:>8}  {r[2]:<9} {str(r[3] or ""):<9} {r[1]}')
conn.close()
