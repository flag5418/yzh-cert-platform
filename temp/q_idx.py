# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
for t in ['cert_enterprise_original_file','cert_enterprise_original_file_version','cert_enterprise_original_upload_task']:
    cur.execute("SHOW INDEX FROM `%s`" % t)
    print('===', t)
    for r in cur.fetchall():
        print(f'   {r[2]:<40} col={r[4]:<22} seq={r[3]} nonuniq={r[1]}')
print()
cur.execute("""SELECT COUNT(*) FROM cert_enterprise_original_file WHERE IsDeleted=0
                AND (MarkdownPath IS NULL OR MarkdownPath='')""")
print('无 Markdown 的文件数 =', cur.fetchone()[0])
cur.execute("""SELECT ConvertStatus, COUNT(*) FROM cert_enterprise_original_file
                WHERE IsDeleted=0 GROUP BY ConvertStatus""")
print('ConvertStatus 分布 =', cur.fetchall())
cur.execute("""SELECT AnalyzeStatus, COUNT(*) FROM cert_enterprise_original_file
                WHERE IsDeleted=0 GROUP BY AnalyzeStatus""")
print('AnalyzeStatus 分布 =', cur.fetchall())
cur.execute("""SELECT COUNT(DISTINCT EnterpriseCode) FROM cert_enterprise_original_file WHERE IsDeleted=0""")
print('有文件的企业数 =', cur.fetchone()[0])
cur.execute("""SELECT EnterpriseCode, StageCode, COUNT(*) FROM cert_enterprise_original_file
                WHERE IsDeleted=0 GROUP BY EnterpriseCode, StageCode""")
print('按企业+阶段分布 =', cur.fetchall())
conn.close()
