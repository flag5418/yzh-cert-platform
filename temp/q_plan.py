# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
for t in ['cert_audit_plan','cert_enterprise_stage','cert_standard_directory_file']:
    cur.execute("SHOW COLUMNS FROM `%s`" % t)
    print('===', t, '->', ', '.join(r[0] for r in cur.fetchall()))
print()
cur.execute("SELECT COUNT(*) FROM cert_audit_plan WHERE IsDeleted=0")
print('计划数 =', cur.fetchone()[0])
cur.execute("SELECT Code,Name,EnterpriseCode,StageCode,Standards FROM cert_audit_plan WHERE IsDeleted=0 LIMIT 5")
for r in cur.fetchall(): print(' ', r)
print()
cur.execute("SELECT COUNT(*) FROM cert_enterprise WHERE IsDeleted=0")
print('企业数 =', cur.fetchone()[0])
cur.execute("SELECT COUNT(*) FROM cert_enterprise_stage")
print('企业-阶段-标准 行数 =', cur.fetchone()[0])
cur.execute("SELECT COUNT(*) FROM cert_enterprise_original_file WHERE IsDeleted=0")
print('原始资料文件数 =', cur.fetchone()[0])
cur.execute("SELECT COUNT(*) FROM cert_standard_directory_file WHERE IsDeleted=0 AND EnterpriseCode IS NOT NULL AND EnterpriseCode<>''")
print('资料库企业侧槽位行数 =', cur.fetchone()[0])
conn.close()
