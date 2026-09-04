#!/bin/bash
# 停止后端
pkill -f "dotnet.*Vue.NetCore" 2>/dev/null
sleep 2

# 清 Redis 菜单缓存（通过 CLI）
redis-cli -p 6380 KEYS "*Menu*" 2>/dev/null | xargs -r redis-cli -p 6380 DEL
redis-cli -p 6380 KEYS "*menu*" 2>/dev/null | xargs -r redis-cli -p 6380 DEL
echo "Redis 缓存已清理"

# 启动后端
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore
dotnet run --project vol.api/VOL.WebApi --urls "http://0.0.0.0:9992" &
echo "后端启动中..."
sleep 5
curl -s http://127.0.0.1:9992/api/User/getVierificationCode > /dev/null && echo "后端已启动" || echo "后端启动中..."
