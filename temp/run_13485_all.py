# -*- coding: utf-8 -*-
"""分批跑完 iso13485 全部叶子条款的属性（未覆盖模式，每批 20 条）"""
import json, time, urllib.request

BASE = 'http://127.0.0.1:9992'

def post(path, body, token=None, timeout=900):
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode(),
                                 headers={'Content-Type': 'application/json'})
    if token: req.add_header('Authorization', 'Bearer ' + token)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return {'_http': e.code, '_body': e.read().decode()[:800]}

tok = post('/api/User/login', {'UserName': 'admin', 'Password': '123456'})['data']['Token']

BATCH = 20
rounds = 0
total_ok = total_fail = total_written = 0

while rounds < 10:
    rounds += 1
    t0 = time.time()
    r = post('/api/Admin/Cert/ClauseAttr/generate', {
        'StandardCode': 'iso13485', 'Overwrite': False, 'MaxCount': BATCH,
    }, tok)
    dt = time.time() - t0
    d = r.get('data') or {}
    print(f'\n=== 第 {rounds} 批 | {dt:.0f}s | success={r.get("success")} | {r.get("message")}')

    if not r.get('success'):
        print('  中止：', r.get('message') or r)
        break

    if d.get('Total', 0) == 0:
        break

    for it in d.get('Items') or []:
        flag = 'OK ' if it['Success'] else 'ERR'
        print(f"  [{flag}] {it['ClauseNumber']:>8}  w={it['Written']} s={it['Skipped']}  {it['Message'][:90]}")
        if not it['Success']:
            print('        RAW:', (it.get('RawOutput') or '')[:400].replace('\n', ' '))

    total_ok += d.get('Succeeded', 0)
    total_fail += d.get('Failed', 0)
    total_written += d.get('Written', 0)

    if d.get('Failed', 0) > 0:
        print('  ⚠️ 有失败，继续下一批（失败条款会在下一批重试，因为未覆盖模式只挑缺属性的）')

print(f'\n===== 合计：成功 {total_ok} 条 / 失败 {total_fail} 条 / 写入 {total_written} 个属性')
