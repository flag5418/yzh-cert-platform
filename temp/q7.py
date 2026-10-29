# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
for no in ['artifact_type','clause_obligation','clause_applicability','clause_severity','process_domain','clause_attr_value_type']:
    cur.execute("SELECT Code,DicName FROM Sys_Dictionary WHERE DicNo=%s AND IsDeleted=0", (no,))
    d = cur.fetchone()
    if not d:
        print(no, '=> 缺失'); continue
    cur.execute("SELECT DicValue,DicName FROM Sys_DictionaryList WHERE DicCode=%s AND IsDeleted=0 ORDER BY OrderNo", (d[0],))
    print(no, '=>', d[1], '|', ', '.join('%s(%s)'%(r[0],r[1]) for r in cur.fetchall()))
conn.close()
