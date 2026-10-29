# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4', autocommit=True)
cur = conn.cursor()
new = ('从这个字典里选**真正相关**的过程码，按相关度排序，输出 JSON 数组。'
       '通常 1~3 个；确有必要（如「设计和开发输出」同时约束设计/采购/生产/检验）可到 4 个。'
       '⛔ 不许自造码，⛔ 不许为了凑数把所有过程都选上（全选等于没选）。')
cur.execute("UPDATE cert_clause_attr_def SET PromptHint=%s, UpdateTime=NOW() WHERE Code='Process'", (new,))
print('updated', cur.rowcount)
conn.close()
