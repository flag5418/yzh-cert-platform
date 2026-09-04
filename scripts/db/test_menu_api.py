#!/usr/bin/env python3
"""测试后端菜单 API"""
import requests, json

# 1. 先登录获取 token
login_data = {"userName": "admin", "password": "admin", "verificationCode": "", "UUID": ""}
try:
    r = requests.post('http://127.0.0.1:9992/api/user/login', json=login_data)
    print(f'登录: {r.status_code}')
    token = r.json().get('data', {}).get('token', '')
    headers = {'Authorization': f'Bearer {token}'}
    
    # 2. 获取菜单
    menu_r = requests.get('http://127.0.0.1:9992/api/Menu/GetMenu', headers=headers)
    print(f'菜单 API: {menu_r.status_code}')
    data = menu_r.json()
    
    # 检查是否包含"基础组件"
    raw = json.dumps(data, ensure_ascii=False)
    if '基础组件' in raw:
        print('仍然包含"基础组件"！')
        # 找到这个菜单
        def find_menu(items, depth=0):
            for item in items:
                if '基础' in str(item.get('menuName', '')):
                    print(f'  找到: {item}')
                if 'children' in item:
                    find_menu(item['children'], depth+1)
        if isinstance(data, list):
            find_menu(data)
        elif isinstance(data, dict) and 'data' in data:
            find_menu(data['data'])
    else:
        print('菜单 API 正常，不包含"基础组件"')
        
except Exception as e:
    print(f'错误: {e}')
