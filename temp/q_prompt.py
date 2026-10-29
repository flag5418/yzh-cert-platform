import pymysql
c = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.', database='yzh_cert_platform', charset='utf8mb4')
cur = c.cursor()
cur.execute("SELECT PromptCode, PromptName, OrgCode, ScopeKey, Status, LENGTH(Template), IsValid, IsDeleted FROM cert_prompt_template ORDER BY PromptCode")
print('cert_prompt_template:')
for r in cur.fetchall():
    print('  ', r)
cur.execute("DESC cert_document_extraction")
print('\ncert_document_extraction 列:')
for r in cur.fetchall():
    print('  ', r[0], r[1], r[2])
cur.execute("SELECT COUNT(*) FROM cert_iso_clause WHERE IsDeleted=0 AND IsValid=1")
print('\ncert_iso_clause 总数:', cur.fetchone())
cur.execute("""SELECT c.StandardCode, COUNT(*) FROM cert_iso_clause c WHERE c.IsDeleted=0 AND c.IsValid=1 GROUP BY c.StandardCode""")
print('按标准:')
for r in cur.fetchall():
    print('  ', r)
c.close()
