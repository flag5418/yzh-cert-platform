# -*- coding: utf-8 -*-
import json, urllib.request

BASE = 'http://127.0.0.1:9992'

def call(path, body=None, token=None):
    data = json.dumps(body or {}).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data,
                                 headers={'Content-Type': 'application/json'})
    if token: req.add_header('Authorization', 'Bearer ' + token)
    if data is None: req.get_method = lambda: 'GET'
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return {'_http': e.code, '_body': e.read().decode()[:400]}

tok = call('/api/User/login', {'UserName': 'admin', 'Password': '123456'})['data']['Token']

cfg = call('/api/Admin/Cert/ClauseAttrDef/config', token=tok)
cols = (cfg.get('data') or {}).get('Columns') or []
print('Columns:')
for c in cols:
    print(f"  {c.get('FieldName'):<14} XsFlag={c.get('XsFlag')} Type={c.get('Type')} Width={c.get('Width')}")

print('\n属性池列表:')
lst = call('/api/Admin/Cert/ClauseAttrDef/filter', {'Page': 1, 'PageSize': 50, 'SortField': 'Sort', 'SortOrder': 'asc'}, tok)
for r in (lst.get('data') or {}).get('Items') or []:
    hint = (r.get('PromptHint') or '').replace('\n', ' ')
    print(f"  {r['Code']:<14} {r['AttrName']:<10} {r['ValueType']:<6} {r.get('DictCode') or '-':<24} hint={hint[:40]}")
