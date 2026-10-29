# -*- coding: utf-8 -*-
"""P4 下线前只读盘点 2（⛔ 只 SELECT）"""
import pymysql

conn = pymysql.connect(host="127.0.0.1", port=3307, user="root",
                       password="Yzh123456.", database="yzh_cert_platform",
                       charset="utf8mb4")
cur = conn.cursor()


def q(title, sql):
    print("=" * 74)
    print(title)
    try:
        cur.execute(sql)
        rows = cur.fetchall()
        if not rows:
            print("  (空)")
        for r in rows:
            print("  ", r)
    except Exception as e:
        print("  !! ", e)


q("0. yzh_queue 列名", "SHOW COLUMNS FROM yzh_queue")

q("1. yzh_queue 状态分布", """
  SELECT TaskType, Status, COUNT(*) AS Cnt
  FROM yzh_queue GROUP BY TaskType, Status ORDER BY TaskType, Status
""")

q("2. 专家端菜单", """
  SELECT Code, ParentCode, MenuName, Url, OrderNo, IsValid
  FROM Sys_Menu WHERE Code LIKE 'MENU_AUD%' ORDER BY OrderNo
""")

q("3. cert_standard_directory_file 列", "SHOW COLUMNS FROM cert_standard_directory_file")

q("4. cert_standard_directory_file 概览", """
  SELECT COUNT(*) AS Total,
         SUM(CASE WHEN IFNULL(EnterpriseCode,'') <> '' THEN 1 ELSE 0 END) AS WithEnt
  FROM cert_standard_directory_file
""")

conn.close()
