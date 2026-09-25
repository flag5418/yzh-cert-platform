#!/usr/bin/env python3
"""测试简化注册 API"""
import requests
import json

BASE = 'http://127.0.0.1:9992'

# 1. 获取机构列表
print('=== 1. 获取机构列表 ===')
r = requests.get(f'{BASE}/api/AuditorAuth/GetOrgList')
print(f'Status: {r.status_code}')
data = r.json()
print(f'Full response: {json.dumps(data, ensure_ascii=False)[:500]}')

if data.get('status') and data.get('data'):
    orgs = data['data']
    print(f'Count: {len(orgs)}')
    print(f'First org: {json.dumps(orgs[0], ensure_ascii=False)}')
    test_org_id = orgs[0]['id']
else:
    print('No orgs found or request failed!')
    exit(1)

# 2. 测试极简注册
print()
print('=== 2. 测试极简注册 ===')
import random
test_user = f'simple_{random.randint(10000,99999)}'
payload = {
    'UserName': test_user,
    'UserPwd': '123456',
    'OrgId': int(test_org_id)
}
print(f'Payload: {json.dumps(payload, ensure_ascii=False)}')

r = requests.post(f'{BASE}/api/AuditorAuth/Register', json=payload)
print(f'Status: {r.status_code}')
data = r.json()
print(f'Response: {json.dumps(data, ensure_ascii=False)}')

# 3. 测试重复账号
print()
print('=== 3. 测试重复账号 ===')
r = requests.post(f'{BASE}/api/AuditorAuth/Register', json=payload)
data = r.json()
print(f'Response: status={data.get("status")}, message={data.get("message")}')

# 4. 测试机构不存在
print()
print('=== 4. 测试机构不存在 ===')
payload2 = {'UserName': 'testx', 'UserPwd': '123456', 'OrgId': 99999}
r = requests.post(f'{BASE}/api/AuditorAuth/Register', json=payload2)
data = r.json()
print(f'Response: status={data.get("status")}, message={data.get("message")}')

# 5. 测试密码太短
print()
print('=== 5. 测试密码太短 ===')
payload3 = {'UserName': 'testy', 'UserPwd': '123', 'OrgId': int(test_org_id)}
r = requests.post(f'{BASE}/api/AuditorAuth/Register', json=payload3)
data = r.json()
print(f'Response: status={data.get("status")}, message={data.get("message")}')

print()
print('=== 测试完成 ===')
