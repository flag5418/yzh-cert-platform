# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4', autocommit=True)
cur = conn.cursor()
new = ('从这个字典里选 1~3 个最相关的过程码，按相关度排序，输出 JSON 数组。'
       '⚠️ **最多 3 个**：超过 3 个只保留最相关的 3 个，输出前数一遍元素个数。'
       '⛔ 不许自造码，⛔ 不许把所有过程都选上（全选等于没选）。')
cur.execute("UPDATE cert_clause_attr_def SET PromptHint=%s, UpdateTime=NOW() WHERE Code='Process'", (new,))
print('updated', cur.rowcount)
conn.close()
