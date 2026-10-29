# -*- coding: utf-8 -*-
"""应用 20261029_clause_attr_prompt_V3.sql（逐语句执行 + 打印验证结果）

★ 为什么不用 mysql 客户端：沙箱没有 mysql CLI；pymysql 逐语句执行能精确报出哪一条失败。
★ 拆分口径：本脚本的 SQL 里**没有内联注释**（`--` 只出现在整行），且提示词正文里
  没有 ASCII 分号 ⇒ 先剔掉整行注释，再按 `;` 拆分是安全的。
"""
import re
import sys
import pymysql

SQL_FILE = 'scripts/db/20261029_clause_attr_prompt_V3.sql'

conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4', autocommit=True)
cur = conn.cursor()

with open(SQL_FILE, encoding='utf-8') as f:
    raw = f.read()

lines = [ln for ln in raw.splitlines() if not ln.lstrip().startswith('--')]
body = '\n'.join(lines)

stmts = [s.strip() for s in body.split(';') if s.strip()]
print('语句数 =', len(stmts))

for i, sql in enumerate(stmts, 1):
    head = re.sub(r'\s+', ' ', sql)[:70]
    try:
        cur.execute(sql)
        rows = cur.fetchall() if cur.description else None
        if rows is not None:
            print(f'[{i}] SELECT -> {head}')
            for r in rows:
                print('     ', r)
        else:
            print(f'[{i}] OK rows={cur.rowcount} -> {head}')
    except Exception as e:
        print(f'[{i}] FAIL -> {head}')
        print('     ', e)
        conn.close()
        sys.exit(1)

conn.close()
print('done')
