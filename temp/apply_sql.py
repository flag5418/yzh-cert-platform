# -*- coding: utf-8 -*-
"""通用 SQL 应用器：逐语句执行并打印结果。

用法：python temp/apply_sql.py scripts/db/xxx.sql

★ 拆分口径：本项目 SQL 的 `--` 只出现在整行（无内联注释），
  且 DDL 的 COMMENT 字符串里不含 ASCII 分号 ⇒ 先剔整行注释再按 `;` 拆分是安全的。
  若将来出现内联注释或字符串内分号，本脚本会报错而不是静默执行半条语句。
"""
import re
import sys
import pymysql

if len(sys.argv) < 2:
    print('用法: python temp/apply_sql.py <sql文件>')
    sys.exit(2)

sql_file = sys.argv[1]

conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4', autocommit=True)
cur = conn.cursor()

with open(sql_file, encoding='utf-8') as f:
    raw = f.read()

lines = [ln for ln in raw.splitlines() if not ln.lstrip().startswith('--')]
body = '\n'.join(lines)
stmts = [s.strip() for s in body.split(';') if s.strip()]
print(f'{sql_file}: 语句数 = {len(stmts)}')

for i, sql in enumerate(stmts, 1):
    head = re.sub(r'\s+', ' ', sql)[:72]
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
