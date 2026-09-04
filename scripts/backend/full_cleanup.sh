#!/bin/bash
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台

# 1. 删除示例菜单（数据库彻底删除）
python3 -c "
import mysql.connector
conn = mysql.connector.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.', database='yzh_cert_platform')
c = conn.cursor()

# 删除基础组件及其子菜单
c.execute('DELETE FROM sys_menu WHERE ParentId = 32')
print(f'删除基础组件子菜单: {c.rowcount} 条')
c.execute('DELETE FROM sys_menu WHERE Menu_Id = 32')
print(f'删除基础组件: {c.rowcount} 条')

# 删除基础页面 (MenuType=1) 及其子菜单
c.execute('DELETE FROM sys_menu WHERE ParentId = 113')
c.execute('DELETE FROM sys_menu WHERE Menu_Id = 113')
print(f'删除基础页面: {c.rowcount} 条')

# 删除所有 MenuType=1 残留
c.execute('DELETE FROM sys_menu WHERE MenuType = 1')

conn.commit()
print('数据库清理完成')
c.close()
conn.close()
"

# 2. 彻底清除 Redis 缓存
echo ""
echo "=== 清除 Redis 缓存 ==="
redis-cli -p 6380 KEYS "*enu*" 2>/dev/null
redis-cli -p 6380 KEYS "*enu*" 2>/dev/null | xargs -r redis-cli -p 6380 DEL

# 3. 停止旧后端进程并重新启动
echo ""
echo "=== 重启后端 ==="
pkill -f "dotnet.*Vue.NetCore" 2>/dev/null
sleep 2

# 4. 重新启动后端
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore
nohup dotnet run --project vol.api/VOL.WebApi --urls "http://0.0.0.0:9992" > /tmp/backend.log 2>&1 &
echo "后端启动中..."
sleep 6

# 5. 验证
curl -s http://127.0.0.1:9992/api/User/getVierificationCode > /dev/null && echo "后端: ✓" || echo "后端: 启动中..."