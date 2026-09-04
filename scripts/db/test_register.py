#!/usr/bin/env python3
"""Test Registration API"""
import json
import urllib.request

BASE = 'http://localhost:9992'

def api_get(path):
    req = urllib.request.Request(f'{BASE}{path}')
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read())

def api_post(path, data):
    body = json.dumps(data).encode()
    req = urllib.request.Request(f'{BASE}{path}', data=body, headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read())

# 1. Get org list
print('=== 1. GetOrgList ===')
result = api_get('/api/AuditorAuth/GetOrgList')
print(f'  status: {result.get("status")}')
if result.get('status') and result.get('data'):
    orgs = result['data']
    print(f'  Found {len(orgs)} organizations:')
    for org in orgs:
        print(f'    id={org.get("id")}, name={org.get("orgName")}')
    # Pick "测试认证机构" (id=1) for testing
    test_org = next((o for o in orgs if o.get('orgName') == '测试认证机构'), orgs[0])
    test_org_id = test_org['id']
else:
    print('  Failed:', result)
    test_org_id = 1

print()
print(f'=== 2. Register (OrgId={test_org_id}) ===')

# 2. Test register with org
import time
ts = int(time.time())
register_data = {
    'UserName': f'testuser_{ts}',
    'UserPwd': 'Test@123456',
    'UserTrueName': '测试用户',
    'PhoneNo': f'138{ts % 100000000:08d}',
    'Email': f'test{ts}@test.com',
    'OrgId': test_org_id
}

print(f'  Request: {register_data}')
try:
    result = api_post('/api/AuditorAuth/Register', register_data)
    print(f'  Response: {json.dumps(result, ensure_ascii=False, indent=2)}')
except Exception as e:
    print(f'  Error: {e}')

print()
print('=== 3. Register with invalid OrgId ===')
bad_data = {
    'UserName': f'baduser_{ts}',
    'UserPwd': 'Test@123456',
    'UserTrueName': '坏用户',
    'PhoneNo': f'139{ts % 100000000:08d}',
    'OrgId': 99999
}
try:
    result = api_post('/api/AuditorAuth/Register', bad_data)
    print(f'  Response: status={result.get("status")}, message={result.get("message")}')
except Exception as e:
    print(f'  Error: {e}')
