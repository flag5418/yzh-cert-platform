# -*- coding: utf-8 -*-
import json, urllib.request
BASE = 'http://127.0.0.1:9992'
def post(path, body, token=None, timeout=600):
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode(),
                                 headers={'Content-Type': 'application/json'})
    if token: req.add_header('Authorization', 'Bearer ' + token)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return {'_http': e.code, '_body': e.read().decode()[:600]}
tok = post('/api/User/login', {'UserName': 'admin', 'Password': '123456'})['data']['Token']
r = post('/api/Admin/Cert/ClauseAttr/generate', {
    'StandardCode': 'iso13485',
    'ClauseCodes': ['0bf1ba323a8dad8018b7a8457fc9c1ea'],
    'Overwrite': True, 'MaxCount': 1}, tok)
print('success =', r.get('success'), '|', r.get('message'))
for it in (r.get('data') or {}).get('Items') or []:
    print(' ', it['ClauseNumber'], it['Success'], it['Message'])
