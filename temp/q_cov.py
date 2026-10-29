# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("""SELECT COUNT(DISTINCT c.Code) FROM cert_iso_clause c
                 JOIN cert_iso_standard s ON s.Code=c.StandardCode
                WHERE s.StandardCode='iso13485' AND c.IsDeleted=0""")
total = cur.fetchone()[0]
cur.execute("""SELECT COUNT(DISTINCT a.ClauseCode) FROM cert_clause_attr a
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0""")
done = cur.fetchone()[0]
cur.execute("""SELECT COUNT(*) FROM cert_clause_attr a
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0""")
cells = cur.fetchone()[0]
cur.execute("""SELECT COUNT(*) FROM cert_clause_attr a
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0
                  AND (a.AttrValue IS NULL OR a.AttrValue='')""")
empty = cur.fetchone()[0]
print(f'条款 {total} 条｜有属性 {done} 条｜属性行 {cells} 格（{cells/9:.0f} 条×9）｜空值 {empty} 格')
