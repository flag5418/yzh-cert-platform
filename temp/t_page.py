# -*- coding: utf-8 -*-
import json, urllib.request
BASE = 'http://127.0.0.1:9992'
def call(path, body=None, token=None):
    data = json.dumps(body or {}).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, headers={'Content-Type': 'application/json'})
    if token: req.add_header('Authorization', 'Bearer ' + token)
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return {'_http': e.code, '_body': e.read().decode()[:300]}

tok = call('/api/User/login', {'UserName': 'admin', 'Password': '123456'})['data']['Token']

opt = call('/api/Admin/Cert/ClauseAttrDef/options', token=tok)
items = opt.get('data') or []
print('属性下拉选项数 =', len(items), '->', [i['Code'] for i in items])

tree = call('/api/Admin/Cert/ClauseAttr/tree/root', {}, tok)
std = tree.get('data') or []
print('树根（标准）数 =', len(std))
for s in std:
    print('  ', s['Name'])

cnt = call('/api/Admin/Cert/ClauseAttr/tree/counts', {}, tok)
m = cnt.get('data') or {}
vals = list(m.values())
print('tree/counts 覆盖条款 =', len(m), '| 每条款属性数取值集合 =', sorted(set(vals)))
