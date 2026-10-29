# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("""SELECT c.ClauseNumber,c.Code FROM cert_iso_clause c
                 JOIN cert_iso_standard s ON s.Code=c.StandardCode
                WHERE s.StandardCode='iso13485' AND c.IsDeleted=0
                  AND c.ClauseNumber IN ('4.1','4.2.1','7.3.4') ORDER BY c.Sort""")
for r in cur.fetchall(): print(r[1], r[0])
conn.close()
