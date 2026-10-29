# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()

print('=== cert_clause_attr_def ===')
cur.execute("SELECT Code,AttrName,ValueType,DictCode,Sort,IsValid FROM cert_clause_attr_def ORDER BY Sort,Id")
for r in cur.fetchall():
    print(r)

print('\n=== 标准 ===')
cur.execute("SELECT Code,StandardCode,StandardName,Abbrev FROM cert_iso_standard WHERE IsDeleted=0")
for r in cur.fetchall():
    print(r)

print('\n=== 条款数 by 标准 ===')
cur.execute("""SELECT s.StandardCode, COUNT(c.Id) FROM cert_iso_standard s
  LEFT JOIN cert_iso_clause c ON c.StandardCode=s.StandardCode AND c.IsDeleted=0
  WHERE s.IsDeleted=0 GROUP BY s.StandardCode""")
for r in cur.fetchall():
    print(r)

print('\n=== cert_clause_attr 覆盖 ===')
cur.execute("""SELECT StandardCode, COUNT(*) AS rows_, COUNT(DISTINCT ClauseNumber) AS clauses_
  FROM cert_clause_attr WHERE IsDeleted=0 GROUP BY StandardCode""")
for r in cur.fetchall():
    print(r)
conn.close()
