#!/usr/bin/env python3
import mysql.connector
conn = mysql.connector.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.', database='yzh_cert_platform')
c = conn.cursor()
c.execute('SELECT Menu_Id, MenuName, Url, MenuType FROM sys_menu WHERE MenuType IN (0,1) ORDER BY MenuType, Menu_Id')
print('=== 剩余菜单 ===')
for r in c.fetchall():
    tname = {0:'前台', 1:'MenuType1', 2:'MenuType2'}.get(r[3], str(r[3]))
    print(f'  [{r[0]:3d}] {r[1]:20s} | Url={str(r[2])[:35]:35s} | Type={tname}')
c.close()
conn.close()
