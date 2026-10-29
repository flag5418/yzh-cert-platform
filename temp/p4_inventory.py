# -*- coding: utf-8 -*-
"""P4 下线前只读盘点（⛔ 只 SELECT，不改任何数据）"""
import pymysql

conn = pymysql.connect(host="127.0.0.1", port=3307, user="root",
                       password="Yzh123456.", database="yzh_cert_platform",
                       charset="utf8mb4")
cur = conn.cursor()


def q(title, sql, args=None):
    print("=" * 70)
    print(title)
    try:
        cur.execute(sql, args or ())
        rows = cur.fetchall()
        if not rows:
            print("  (空)")
        for r in rows:
            print("  ", r)
        return rows
    except Exception as e:
        print("  !! ", e)
        return []


# 1) 未完成的队列任务（P4 停用老执行器前必须先确认）
q("1. yzh_queue 未完成任务（按 TaskType 分组）", """
  SELECT TaskType, Status, COUNT(*) AS Cnt
  FROM yzh_queue
  WHERE Status NOT IN ('success', 'failed', 'canceled', 'cancelled')
  GROUP BY TaskType, Status ORDER BY TaskType, Status
""")

q("1b. yzh_queue 全部状态分布", """
  SELECT TaskType, Status, COUNT(*) AS Cnt
  FROM yzh_queue GROUP BY TaskType, Status ORDER BY TaskType, Status
""")

# 2) 老表 vs 新表行数
for t in ("cert_enterprise_original", "cert_enterprise_original_file",
          "cert_enterprise_original_version", "cert_enterprise_doc",
          "cert_enterprise_doc_version", "cert_document_extraction",
          "cert_enterprise_doc_upload_task"):
    q(f"2. {t} 行数", f"SELECT COUNT(*) FROM {t}")

# 3) 菜单
q("3. 专家端菜单（MENU_AUD_*）", """
  SELECT Code, ParentCode, MenuName, Url, OrderNo, IsValid
  FROM Sys_Menu WHERE Code LIKE 'MENU_AUD%' ORDER BY OrderNo
""")

# 4) cert_standard_directory_file 企业侧行
q("4. cert_standard_directory_file 按用途分组", """
  SELECT COUNT(*) FROM cert_standard_directory_file
""")

print("=" * 70)
conn.close()
