#!/usr/bin/env python3
"""彻底删除所有示例/废弃菜单 + MES 菜单"""
import mysql.connector

conn = mysql.connector.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform'
)
c = conn.cursor()

# 先列出待删除
c.execute("SELECT Menu_Id, MenuName, ParentId FROM sys_menu WHERE Menu_Id IN (8, 32, 36, 64, 65, 91, 137, 138, 139, 296, 297, 298, 299, 300, 301, 302, 113, 115, 125) OR ParentId IN (32, 64, 137, 113) ORDER BY ParentId, Menu_Id")
rows = c.fetchall()
print(f'待删除菜单 {len(rows)} 条:')
for r in rows:
    print(f'  [{r[0]:3d}] {r[1]:15s} (Parent={r[2]})')

# 删除
c.execute('DELETE FROM sys_menu WHERE ParentId IN (32, 64, 137, 113)')
child_count = c.rowcount
print(f'\n删除第三层子菜单: {child_count} 条')

c.execute('DELETE FROM sys_menu WHERE Menu_Id IN (32, 64, 137, 113)')
parent_count = c.rowcount
print(f'删除第二层菜单: {parent_count} 条')

c.execute('DELETE FROM sys_menu WHERE MenuType = 1')
print(f'删除 MenuType=1: {c.rowcount} 条')

# 删除 MES 业务
c.execute('DELETE FROM sys_menu WHERE ParentId IN (SELECT Menu_Id FROM (SELECT Menu_Id FROM sys_menu WHERE ParentId = 235) tmp)')
print(f'删除 MES 三级菜单: {c.rowcount} 条')
c.execute('DELETE FROM sys_menu WHERE Menu_Id = 235 OR ParentId = 235')
print(f'删除 MES 菜单: {c.rowcount} 条')

# 删除 Vol 自带 demo
delete_list = ['图表%', 'MES%', '多页签%', '一对多%', '主从%', '文本编辑%', '树形结构%', '移动端开发%', '基础组件%', '基础页面%', '代码生成%', '定时任务%']
for pattern in delete_list:
    c.execute("DELETE FROM sys_menu WHERE MenuName LIKE %s", (pattern,))
    if c.rowcount > 0:
        print(f'删除 LIKE "{pattern}": {c.rowcount} 条')

conn.commit()

# 验证
c.execute("SELECT COUNT(*) FROM sys_menu WHERE MenuName LIKE '%基础%' OR MenuName LIKE '%图表%' OR MenuName LIKE '%MES%' OR MenuName LIKE '%代码生成%' OR MenuName LIKE '%定时任务%'")
remaining = c.fetchone()[0]
print(f'\n剩余废弃菜单: {remaining} 条')

c.execute('SELECT Menu_Id, MenuName FROM sys_menu WHERE ParentId = 0 ORDER BY Menu_Id')
print('\n当前顶级菜单:')
for r in c.fetchall():
    print(f'  [{r[0]:3d}] {r[1]}')

c.close()
conn.close()
