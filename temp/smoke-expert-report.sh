#!/bin/bash
# 加载本地口令（⛔ 禁止把口令写进脚本/文档）
for _e in "$(dirname "$0")/../../docker/.env" "$(dirname "$0")/../docker/.env" "$(dirname "$0")/../../../docker/.env" "$(dirname "$0")/docker/.env"; do
  [ -f "$_e" ] && set -a && . "$_e" && set +a && break
done
# ══════════════════════════════════════════════════════════════════
# 报告生成链路 —— S4 冒烟（临时脚本，位于 temp/）
# 覆盖：锁判定（REPORT_GENERATE 与 NC_CHECK 互不阻塞）→ 候选 → 创建
#       → 提交 → 队列启动 → 结果树（report）→ 列表 → 降级/待撰写 → 修改 → 导出
# ══════════════════════════════════════════════════════════════════
set -u
API="http://localhost:9992/api"
J="Content-Type: application/json"

ENT="42833c3d3fe241b1bcfa707f6b92e4c3"
STAGE="29c1bcc3a18942e1865b2497a0262504"
STD="846dec4b-c534-4983-94e6-8cf04982b7d9"

echo "── 0. 登录 ──"
TOKEN=$(curl --noproxy '*' -s -X POST "$API/User/login" -H "$J" \
  -d '{"UserName":"wzm","Password":"123456"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['Token'])")
AUTH="Authorization: Bearer $TOKEN"
echo "  ✓ Token ${#TOKEN}"

post(){ curl --noproxy '*' -s --max-time 60 -X POST "$API/$1" -H "$J" -H "$AUTH" -d "$2"; }
jq(){ python3 -c "
import sys,json
try: d=json.load(sys.stdin)
except Exception as e: print('PARSE_ERR',e); sys.exit()
$1"; }

echo "── 1. 锁判定（REPORT_GENERATE；NC 任务不应阻塞它） ──"
LC=$(post "Auditor/ExpertTask/lock-check" "{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"TaskType\":\"REPORT_GENERATE\"}")
echo "$LC" | jq "
d=d.get('data') or {}
print(f\"  CanCreate={d.get('CanCreate')} CandidateCount={d.get('CandidateCount')} Message={d.get('Message')}\")
print('  ✓ 分类锁互不干扰' if d.get('CanCreate') else '  ✗ 被误阻塞')
"

echo "── 2. 候选章节 ──"
CANDS=$(post "Auditor/ExpertTask/candidates" "{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"TaskType\":\"REPORT_GENERATE\",\"StandardCodes\":[\"$STD\"]}")
echo "$CANDS" | jq "
d=d.get('data') or []
print(f\"  候选数={len(d) if isinstance(d,list) else 0}\")
for c in (d if isinstance(d,list) else []):
    print(f\"   · {c.get('ItemNumber')} {c.get('ItemName')} HasWorkflow={c.get('HasWorkflow')} Suggested={c.get('Suggested')}\")
"

echo "── 3. 创建报告任务 ──"
CR=$(post "Auditor/ExpertTask/create" "{\"TaskName\":\"S4冒烟-复审报告\",\"TaskType\":\"REPORT_GENERATE\",\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"ScopeType\":\"FULL\",\"TaskSource\":\"NEW\",\"StandardCodes\":[\"$STD\"],\"Remark\":\"S4自测-报告\"}")
echo "$CR" | jq "
d=d.get('data') or {}
print(f\"  success={d.get('success')} err={d.get('err')}\")
print(f\"  TaskCode={d.get('TaskCode')} 标准={d.get('StandardCount')} 章节={d.get('ItemCount')} 队列={d.get('QueueCount')}\")
print('  Summary:',d.get('Summary'))
"
TASK=$(echo "$CR" | python3 -c "import sys,json;print((json.load(sys.stdin).get('data') or {}).get('TaskCode',''))" 2>/dev/null)
[ -z "$TASK" ] && { echo "  ✗ 创建失败，终止"; exit 1; }

echo "── 4. 提交 + 启动 ──"
post "Auditor/ExpertTask/submit" "{\"TaskCode\":\"$TASK\"}" | jq "print('  submit:',d.get('success'),d.get('message') or d.get('err'))"
DT=$(post "Auditor/ExpertTask/detail" "{\"TaskCode\":\"$TASK\"}")
QCODE=$(echo "$DT" | python3 -c "
import sys,json
d=json.load(sys.stdin).get('data') or {}
qs=d.get('Queues') or []
print(qs[0].get('Code','') if qs else '')" 2>/dev/null)
post "Auditor/ExpertTask/queue/start" "{\"QueueCode\":\"$QCODE\"}" | jq "print('  start:',d.get('success'),d.get('message') or d.get('err'))"
echo "  （等待执行器…）"; sleep 12

echo "── 5. 结果树（report） ──"
post "Auditor/ExpertResult/tree" '{"Type":"report","Full":true}' | python3 -c "
import sys,json
d=json.load(sys.stdin); nodes=d.get('data') or []
print(f\"  success={d.get('success')} 根节点数={len(nodes)}\")
def walk(ns,lv):
    for n in ns:
        print('   '+'  '*lv+f\"[{n.get('NodeType')}] {n.get('Name')} total={n.get('TotalCount')} pending={n.get('PendingCount')}\")
        walk(n.get('Children') or [], lv+1)
walk(nodes,0)
"

echo "── 6. 结果列表 ──"
RL=$(post "Auditor/ExpertResult/list" "{\"Type\":\"report\",\"TaskCode\":\"$TASK\",\"Page\":1,\"PageSize\":50}")
echo "$RL" | jq "
d=d.get('data') or {}
rows=d.get('Rows') or []
print(f\"  Total={d.get('Total')}\")
for r in rows:
    print(f\"   · 章节号={r.get('ClauseNumber')} {r.get('ItemName')} 自动={r.get('AutoStatus')} 生成方式={r.get('GenerationMode')} 结论={r.get('ConclusionLabel')} 来源={r.get('SourceLabel')} 正文={(r.get('ContentText') or '')[:40]}\")
"
echo "$RL" | python3 -c "
import sys,json
rows=(json.load(sys.stdin).get('data') or {}).get('Rows') or []
open('/tmp/s4_report_rows.json','w').write(json.dumps(rows,ensure_ascii=False))
print('  已保存',len(rows),'行')"

echo "── 7. 详情（子任务状态应随队列收尾推进） ──"
post "Auditor/ExpertTask/detail" "{\"TaskCode\":\"$TASK\"}" | python3 -c "
import sys,json
d=json.load(sys.stdin)['data']
t=d['Task']
print(f\"  任务 {t['TaskNumber']} {t['ExecStatus']} 进度={t['Progress']} 待审批/总={d['PendingReviewCount']}/{d['TotalResultCount']}\")
for s in d['Standards']:
    print(f\"   子任务 {s['StandardCode'][:12]} {s['ExecStatus']} 项={s['ItemCount']} 完成={s['DoneCount']} 跳过={s['SkippedCount']} 进度={s['Progress']} 队列={s['QueueStatus']}\")
for q in d['Queues']:
    print(f\"   队列 {q['Code'][:12]} {q['QueueStatus']} {q['DoneCount']}/{q['TotalCount']}\")
"

echo "── 8. 修改（报告正文） ──"
FIRST=$(python3 -c "
import json
rows=json.load(open('/tmp/s4_report_rows.json'))
print(next((r['Code'] for r in rows if r.get('Code')),''))")
if [ -n "$FIRST" ]; then
  MODJSON=$(python3 -c "
import json
print(json.dumps({'Type':'report','Code':'$FIRST','ContentText':'S4冒烟：专家撰写的报告章节正文','ReviewRemark':'冒烟'},ensure_ascii=False))")
  post "Auditor/ExpertResult/modify" "$MODJSON" | jq "print('  modify:',d.get('success'),d.get('message') or d.get('err'))"
fi

echo "── 9. 导出 ──"
curl --noproxy '*' -s --max-time 30 -D /tmp/s4_rhdr.txt -o /tmp/s4_rexport.csv -X POST \
  "$API/Auditor/ExpertResult/export" -H "$J" -H "$AUTH" \
  -d "{\"Type\":\"report\",\"TaskCode\":\"$TASK\"}"
echo "  Content-Type: $(python3 -c "
import io
for l in io.open('/tmp/s4_rhdr.txt',encoding='utf-8',errors='ignore'):
    if l.lower().startswith('content-type'): print(l.strip())
")"
echo "  行数: $(wc -l < /tmp/s4_rexport.csv)"
python3 -c "
import io
for i,l in enumerate(io.open('/tmp/s4_rexport.csv',encoding='utf-8-sig')):
    if i<4: print('   |',l.rstrip())
"

echo ""
echo "── 10. 日志 ──"
post "Auditor/ExpertTask/logs" "{\"TaskCode\":\"$TASK\",\"Take\":30}" | python3 -c "
import sys,json
rows=json.load(sys.stdin).get('data') or []
print(f'  日志数={len(rows)}')
for l in rows[:12]:
    print(f\"   · {l.get('OperateTime','')[:19]} [{l.get('LogLevel')}] {l.get('LogAction')} {l.get('Message') or ''}\")
"

echo ""
echo "── 11. 数据库核对 ──"
docker exec yzh-mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" --default-character-set=utf8mb4 -t -B yzh_cert_platform -e "
SELECT ExecStatus, IFNULL(ActiveLockKey,'<NULL=已解锁>') AS LockKey, Progress FROM cert_expert_task WHERE Code='$TASK';
SELECT ExecStatus, ItemCount, DoneCount, SkippedCount, FailedCount, Progress FROM cert_expert_task_standard WHERE TaskCode='$TASK';
SELECT AutoStatus, SkipCategory, GenerationMode FROM cert_expert_report_result WHERE TaskCode='$TASK';
SELECT ItemStatus, COUNT(*) n FROM cert_expert_task_queue_item WHERE TaskCode='$TASK' GROUP BY ItemStatus;
" 2>/dev/null

echo "TASK=$TASK"; echo "$TASK" > /tmp/s4_report_task.txt
