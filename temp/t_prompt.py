# -*- coding: utf-8 -*-
import json, urllib.request

BASE = 'http://127.0.0.1:9992'

def post(path, body, token=None):
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode(),
                                 headers={'Content-Type': 'application/json'})
    if token: req.add_header('Authorization', 'Bearer ' + token)
    try:
        with urllib.request.urlopen(req) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return {'_http': e.code, '_body': e.read().decode()[:500]}

def get(path, token):
    req = urllib.request.Request(BASE + path, headers={'Authorization': 'Bearer ' + token})
    try:
        with urllib.request.urlopen(req) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return {'_http': e.code, '_body': e.read().decode()[:500]}

tok = post('/api/User/login', {'UserName': 'admin', 'Password': '123456'})['data']['Token']
print('token ok, len', len(tok))

p = get('/api/Admin/Cert/ClauseAttr/prompt', tok)
if not p.get('success'):
    print('prompt FAIL', p); raise SystemExit(1)
d = p['data']
print('Stored =', d['Stored'], '| AttrCount =', d['AttrCount'], '| TemplateLen =', len(d['Template']))
print('Template 含 attr_pool =', '{{attr_pool}}' in d['Template'])
print('Template 含硬编码字段 ClauseIntent =', 'ClauseIntent' in d['Template'])
print('--- 属性清单段 ---')
print(d['AttrPoolSection'])
