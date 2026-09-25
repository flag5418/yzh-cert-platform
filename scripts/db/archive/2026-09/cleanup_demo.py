#!/usr/bin/env python3
"""彻底删除示例菜单和页面（数据库 + 路由文件清理）"""
import subprocess
import os

# === 1. 数据库删除示例菜单 ===
delete_sql = """
-- 删除子菜单
DELETE FROM sys_menu WHERE ParentId IN (32, 113) AND MenuType IN (0, 1);
-- 删除父菜单
DELETE FROM sys_menu WHERE Menu_Id IN (32, 113);
-- 删除其他 MenuType=1 的 demo 数据
DELETE FROM sys_menu WHERE MenuType = 1 AND Menu_Id NOT IN (SELECT Menu_Id FROM sys_menu WHERE MenuType = 0);
"""

sql_file = '/tmp/delete_demo_menus.sql'
with open(sql_file, 'w') as f:
    f.write(delete_sql)

print('=== 数据库删除示例菜单 ===')
result = subprocess.run([
    'python3', '-c', f"""
import mysql.connector
conn = mysql.connector.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.', database='yzh_cert_platform')
c = conn.cursor()
# 查出现有禁用菜单
c.execute('SELECT Menu_Id, MenuName FROM sys_menu WHERE Menu_Id IN (8,32,36,91,113,115,125,296,297,298,299,300,301,302)')
rows = c.fetchall()
print(f'找到 {{len(rows)}} 条待删除菜单:')
for r in rows:
    print(f'  [{{r[0]}}] {{r[1]}}')

# 删除
c.execute('DELETE FROM sys_menu WHERE ParentId = 32')
print(f'删除基础组件子菜单: {{c.rowcount}} 条')
c.execute('DELETE FROM sys_menu WHERE Menu_Id = 32')
print(f'删除基础组件: {{c.rowcount}} 条')
c.execute('DELETE FROM sys_menu WHERE Menu_Id = 113 OR ParentId = 113')
print(f'删除基础页面: {{c.rowcount}} 条')
c.execute('DELETE FROM sys_menu WHERE MenuType = 1')
print(f'删除其他 MenuType=1: {{c.rowcount}} 条')
conn.commit()

# 验证
c.execute('SELECT Menu_Id, MenuName FROM sys_menu WHERE Enable = 0 ORDER BY Menu_Id')
remaining = c.fetchall()
print(f'\\n剩余禁用菜单: {{len(remaining)}} 条')
for r in remaining:
    print(f'  [{{r[0]}}] {{r[1]}}')

c.close()
conn.close()
"""
], capture_output=True, text=True, cwd='/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台')
print(result.stdout)
if result.stderr:
    print(f'ERROR: {result.stderr}')

print('\\n=== 需要删除的Vol页面 ===')
vol_pages_to_remove = [
    'views/builder/',          # 代码生成器
    'views/formDraggable/',    # 表单设计（演示）
    'views/signalR/',          # 消息推送演示
    'views/index/',            # Vol 默认首页（我们有自己的）
]

views_dir = '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/views'
for p in vol_pages_to_remove:
    full = os.path.join(views_dir, p)
    exists = os.path.exists(full)
    print(f'  {p}: {"存在" if exists else "不存在"}')
