# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4', autocommit=True)
cur = conn.cursor()
cur.execute("""UPDATE Sys_DictionaryList SET DicName='资料辅助'
                WHERE DicCode='CLAUSE_DOC_CHECK_MODE' AND DicValue='optional' AND DicName<>'资料辅助'""")
print('updated', cur.rowcount)
cur.execute("""SELECT c.ClauseNumber, c.Code FROM cert_iso_clause c
                 JOIN cert_iso_standard s ON s.Code=c.StandardCode
                WHERE s.StandardCode='iso13485' AND c.IsDeleted=0
                  AND c.ClauseNumber IN ('4.2.3','6.2','7.5.1','8.2.1','5.5.1')""")
for r in cur.fetchall(): print(r)
conn.close()
