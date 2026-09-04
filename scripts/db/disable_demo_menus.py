#!/usr/bin/env python3
"""执行禁用示例菜单 SQL"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

statements = [
    # 禁用基础组件子菜单
    "UPDATE sys_menu SET Enable = 0, Modifier = 'system_cleanup', ModifyDate = NOW() WHERE ParentId = 32 AND Enable = 1",
    # 禁用基础组件自身
    "UPDATE sys_menu SET Enable = 0, Modifier = 'system_cleanup', ModifyDate = NOW() WHERE Menu_Id = 32",
    # 禁用基础页面 demos (MenuType=1)
    "UPDATE sys_menu SET Enable = 0, Modifier = 'system_cleanup', ModifyDate = NOW() WHERE (Menu_Id = 113 OR ParentId = 113) AND MenuType = 1",
]

for stmt in statements:
    cursor.execute(stmt)
    print(f'执行: {stmt[:60]}... => 影响 {cursor.rowcount} 行')

conn.commit()

print('\n=== 禁用后的菜单 ===')
cursor.execute('SELECT Menu_Id, MenuName, Url, Enable FROM sys_menu WHERE Enable = 0 ORDER BY Menu_Id')
for r in cursor.fetchall():
    print(f'  [{r[0]:3d}] {r[1]:20s} | Url={str(r[2])[:40]:40s} | Enable={r[3]}')

cursor.close()
conn.close()
