# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
for t,cols in [('cert_enterprise',['Code','OrgCode']),
               ('cert_enterprise_original_file',['Code','EnterpriseCode','OrgCode','StageCode','UploadTaskCode']),
               ('cert_iso_standard',['Code','StandardCode']),
               ('cert_cert_stage',['Code','StageCode'])]:
    cur.execute("SHOW COLUMNS FROM `%s`" % t)
    m = {r[0]:(r[1],r[2]) for r in cur.fetchall()}
    print(t, {c:m.get(c) for c in cols})
conn.close()
