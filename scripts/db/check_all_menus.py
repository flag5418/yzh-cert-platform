#!/usr/bin/env python3
"""查询所有菜单"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
cursor = conn.cursor()

cursor.execute('SELECT Menu_Id, MenuName, Url, ParentId, Enable, MenuType, OrderNo FROM sys_menu ORDER BY MenuType, OrderNo')
rows = cursor.fetchall()

current_type = None
for row in rows:
    mid, name, url, pid, enable, mtype, order = row
    type_name = {1: 'MenuType=1', 2: 'MenuType=2(APP?)', 3: 'MenuType=3'}.get(mtype, f'MenuType={mtype}')
    if type_name != current_type:
        current_type = type_name
        print(f'\n--- {type_name} ---')
    print(f'  [{mid:3d}] {name:20s} | Url={str(url)[:40]:40s} | Pid={pid:3d} | Enable={enable}')

cursor.close()
conn.close()
