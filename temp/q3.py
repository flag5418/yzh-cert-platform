# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()

print('=== 条款数 by 标准 ===')
cur.execute("""SELECT s.StandardCode, COUNT(c.Id) FROM cert_iso_standard s
  LEFT JOIN cert_iso_clause c ON c.StandardCode=s.StandardCode AND c.IsDeleted=0
  WHERE s.IsDeleted=0 GROUP BY s.StandardCode""")
for r in cur.fetchall(): print(r)

print('\n=== 条款属性覆盖（按标准） ===')
cur.execute("""SELECT a.StandardCode, COUNT(*) rows_, COUNT(DISTINCT a.ClauseCode) clauses_
  FROM cert_clause_attr a WHERE a.IsDeleted=0 GROUP BY a.StandardCode""")
for r in cur.fetchall(): print(r)

print('\n=== iso13485 各属性覆盖条款数 ===')
cur.execute("""SELECT a.AttrCode, COUNT(DISTINCT a.ClauseCode)
  FROM cert_clause_attr a JOIN cert_iso_standard s ON s.Code=a.StandardCode
  WHERE s.StandardCode='iso13485' AND a.IsDeleted=0 GROUP BY a.AttrCode""")
for r in cur.fetchall(): print(r)

print('\n=== iso13485 有属性的条款 ===')
cur.execute("""SELECT c.ClauseNumber, COUNT(a.Id)
  FROM cert_iso_clause c JOIN cert_iso_standard s ON s.Code=c.StandardCode
  LEFT JOIN cert_clause_attr a ON a.ClauseCode=c.Code AND a.IsDeleted=0
  WHERE s.StandardCode='iso13485' AND c.IsDeleted=0
  GROUP BY c.ClauseNumber, c.Sort ORDER BY c.Sort""")
for r in cur.fetchall(): print(r)
conn.close()
