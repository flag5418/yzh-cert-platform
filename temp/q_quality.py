# -*- coding: utf-8 -*-
"""1.5 抽查：DocCheckMode / Obligation / Applicability / Severity / Process 分布"""
import json, pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()

def dist(attr):
    cur.execute("""SELECT a.AttrValue, COUNT(*) FROM cert_clause_attr a
                    WHERE a.StandardCode='iso13485' AND a.IsDeleted=0 AND a.AttrCode=%s
                    GROUP BY a.AttrValue ORDER BY COUNT(*) DESC""", (attr,))
    return cur.fetchall()

for attr in ['DocCheckMode', 'Obligation', 'Applicability', 'Severity']:
    rows = dist(attr)
    print(f'{attr}: ' + ' | '.join(f'{v or "(空)"}×{c}' for v, c in rows))

print('\nProcess 元素数分布:')
cur.execute("""SELECT a.AttrValue FROM cert_clause_attr a
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0 AND a.AttrCode='Process'""")
from collections import Counter
cnt = Counter()
bad = []
for (v,) in cur.fetchall():
    try:
        arr = json.loads(v or '[]')
        n = len(arr) if isinstance(arr, list) else -1
        cnt[n] += 1
        if not isinstance(arr, list) or n == 0 or n > 3:
            bad.append((n, v))
    except Exception:
        cnt['解析失败'] += 1
        bad.append(('ERR', v))
print('  ', dict(sorted(cnt.items(), key=lambda x: str(x[0]))))
if bad:
    print('  异常样本:', bad[:5])

print('\nDocCheckMode=none 的条款:')
cur.execute("""SELECT c.ClauseNumber, c.Title FROM cert_clause_attr a
                 JOIN cert_iso_clause c ON c.Code=a.ClauseCode
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0
                  AND a.AttrCode='DocCheckMode' AND a.AttrValue='none'
                ORDER BY c.Sort""")
for r in cur.fetchall(): print('  ', r[0], r[1])

print('\nArtifacts 首句是否输出契约（应含「只输出 JSON 数组」）:')
cur.execute("""SELECT COUNT(*), SUM(a.AttrValue LIKE '只输出 JSON 数组%')
                 FROM cert_clause_attr a
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0 AND a.AttrCode='Artifacts'""")
print('  ', cur.fetchone())
print('EvidenceChain 首句:')
cur.execute("""SELECT COUNT(*), SUM(a.AttrValue LIKE '只输出 JSON 数组%')
                 FROM cert_clause_attr a
                WHERE a.StandardCode='iso13485' AND a.IsDeleted=0 AND a.AttrCode='EvidenceChain'""")
print('  ', cur.fetchone())
conn.close()
