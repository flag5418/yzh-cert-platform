# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("""SELECT c.Code,c.ClauseNumber,c.Title,c.ParentCode FROM cert_iso_clause c
                 JOIN cert_iso_standard s ON s.Code=c.StandardCode
                WHERE s.StandardCode='iso13485' AND c.IsDeleted=0 ORDER BY c.Sort""")
rows = cur.fetchall()
bycode = {r[0]: r for r in rows}
parents = {r[3] for r in rows if r[3]}
leaves = [r for r in rows if r[0] not in parents]
print('全部', len(rows), '｜非叶', len(parents & set(bycode)), '｜叶子', len(leaves))

cur.execute("""SELECT DISTINCT ClauseCode FROM cert_clause_attr
                WHERE StandardCode='iso13485' AND IsDeleted=0""")
have = {r[0] for r in cur.fetchall()}
miss = [r for r in leaves if r[0] not in have]
print('叶子缺属性:', len(miss))
for r in miss:
    print('  ', r[1], r[2], '| 正文长度', len((r and '') or ''))
    cur.execute("SELECT CHAR_LENGTH(Description) FROM cert_iso_clause WHERE Code=%s", (r[0],))
    print('      desc len =', cur.fetchone()[0])

print('\n--- 首句不符合契约的 Artifacts/EvidenceChain ---')
cur.execute("""SELECT c.ClauseNumber, a.AttrCode, LEFT(a.AttrValue,80)
                 FROM cert_clause_attr a JOIN cert_iso_clause c ON c.Code=a.ClauseCode
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0
                  AND a.AttrCode IN ('Artifacts','EvidenceChain')
                  AND a.AttrValue NOT LIKE '只输出 JSON 数组%'""")
for r in cur.fetchall(): print('  ', r)
conn.close()
