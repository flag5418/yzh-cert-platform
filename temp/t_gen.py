# -*- coding: utf-8 -*-
import json, sys, urllib.request

BASE = 'http://127.0.0.1:9992'

def post(path, body, token=None):
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode(),
                                 headers={'Content-Type': 'application/json'})
    if token: req.add_header('Authorization', 'Bearer ' + token)
    try:
        with urllib.request.urlopen(req, timeout=600) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return {'_http': e.code, '_body': e.read().decode()[:800]}

tok = post('/api/User/login', {'UserName': 'admin', 'Password': '123456'})['data']['Token']

codes = sys.argv[1:]
r = post('/api/Admin/Cert/ClauseAttr/generate', {
    'StandardCode': 'iso13485',
    'ClauseCodes': codes,
    'Overwrite': False,
    'MaxCount': len(codes),
}, tok)

print('success =', r.get('success'), '| message =', r.get('message'))
d = r.get('data') or {}
print('PromptScope =', d.get('PromptScope'), '| Model =', d.get('Model'),
      '| Total =', d.get('Total'), 'Written =', d.get('Written'), 'Skipped =', d.get('Skipped'))
for it in d.get('Items') or []:
    print(f"  - {it['ClauseNumber']} {it['Title'][:24]} | ok={it['Success']} w={it['Written']} s={it['Skipped']} | {it['Message']}")
    if not it['Success']:
        print('    RAW:', (it.get('RawOutput') or '')[:600])
