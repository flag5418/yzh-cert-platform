#!/usr/bin/env python3
import mysql.connector
conn = mysql.connector.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.', database='yzh_cert_platform')
c = conn.cursor()
c.execute("SELECT Menu_Id, MenuName FROM sys_menu WHERE MenuName LIKE '%基础%' OR MenuName LIKE '%图表%' OR Menu_Id = 32")
for r in c.fetchall():
    print(f'[{r[0]}] {r[1]}')

c.execute("SELECT Menu_Id, MenuName, ParentId FROM sys_menu ORDER BY Menu_Id")
print('\n=== ALL MENUS ===')
for r in c.fetchall():
    print(f'[{r[0]:3d}] {r[1]:20s} Parent={r[2]}')
c.close()
conn.close()
