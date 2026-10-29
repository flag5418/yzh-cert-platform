# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("SHOW TABLES")
allt = [r[0] for r in cur.fetchall()]
print('表总数', len(allt))
for kw in ['enterprise_doc','enterprise_file','enterprise_original','document_extraction','doc_extract']:
    print(kw, '->', [t for t in allt if kw in t])
conn.close()
