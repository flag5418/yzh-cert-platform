# -*- coding: utf-8 -*-
import sys, pymysql
num = sys.argv[1]
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("""SELECT c.Code, c.Title, LEFT(c.Description,120) FROM cert_iso_clause c
                 JOIN cert_iso_standard s ON s.Code=c.StandardCode
                WHERE s.StandardCode='iso13485' AND c.ClauseNumber=%s AND c.IsDeleted=0""", (num,))
row = cur.fetchone()
print('条款', num, row[1])
print('正文:', (row[2] or '').replace('\n',' '))
print()
cur.execute("""SELECT AttrCode,ValueSource,AttrValue FROM cert_clause_attr
                WHERE ClauseCode=%s AND IsDeleted=0 ORDER BY Sort""", (row[0],))
for a, vs, v in cur.fetchall():
    v = v if v is not None else ''
    print(f'--- {a} [{vs}]')
    print(v if len(v) < 900 else v[:900] + ' …')
conn.close()
