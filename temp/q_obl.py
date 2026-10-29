# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("""SELECT c.ClauseNumber, a.AttrValue, LEFT(c.Description,60)
                 FROM cert_clause_attr a JOIN cert_iso_clause c ON c.Code=a.ClauseCode
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0 AND a.AttrCode='Obligation'
                  AND (c.Description LIKE '%宜%' OR c.Description LIKE '%可%')
                ORDER BY c.Sort LIMIT 12""")
rows = cur.fetchall()
print('正文含 宜/可 的条款共', len(rows), '条（限 12 展示）')
for r in rows:
    print(f'  {r[0]:>8} Obligation={r[1]:<7} {r[2]}')
conn.close()
