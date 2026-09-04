#!/usr/bin/env python3
"""清除 Redis 菜单缓存并重启后端"""
import redis
import subprocess
import time

# 1. 清除菜单缓存
r = redis.Redis(host='127.0.0.1', port=6380, db=0)
keys_to_delete = r.keys('*Menu*') + r.keys('*menu*')
if keys_to_deleted := keys_to_delete:
    r.delete(*keys_to_delete)
    print(f'清除 Redis 缓存: {len(keys_to_delete)} 个 key')
else:
    print('无 Redis 菜单缓存需要清除')

# 2. 停止旧后端进程
print('\n停止旧后端进程...')
subprocess.run(['pkill', '-f', 'dotnet.*Vue.NetCore'], capture_output=True)
time.sleep(2)

# 3. 重新启动后端
print('启动后端...')
subprocess.Popen(
    ['dotnet', 'run', '--project', 'vol.api/VOL.WebApi', '--urls', 'http://0.0.0.0:9992'],
    cwd='/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore',
    stdout=subprocess.DEVNULL,
    stderr=subprocess.DEVNULL
)
print('后端正在启动中...')
