#!/bin/bash
# 加载本地口令（⛔ 禁止把口令写进脚本/文档）
for _e in "$(dirname "$0")/../../docker/.env" "$(dirname "$0")/../docker/.env" "$(dirname "$0")/../../../docker/.env" "$(dirname "$0")/docker/.env"; do
  [ -f "$_e" ] && set -a && . "$_e" && set +a && break
done
# ══════════════════════════════════════════════════════════════════
# 专家任务系统 —— S4 接口冒烟（临时脚本，位于 temp/，不入库）
# 覆盖：列表 → 锁判定 → 候选 → 创建 → 锁拒绝 → 提交 → 队列启动
#       → 详情/日志/缺口 → 结果树/列表/导出
# ══════════════════════════════════════════════════════════════════
set -u
API="http://localhost:9992/api"
C="curl --noproxy * -s --max-time 30"
J="Content-Type: application/json"

WS="66bbf57219d74e21bf164b7e42380c19"
ENT="42833c3d3fe241b1bcfa707f6b92e4c3"
STAGE="29c1bcc3a18942e1865b2497a0262504"
STD="846dec4b-c534-4983-94e6-8cf04982b7d9"

ok(){ echo "  ✓ $1"; }
no(){ echo "  ✗ $1"; FAIL=$((FAIL+1)); }
FAIL=0

# ── 0. 登录 ──
echo "── 0. 登录 ──"
LOGIN=$(curl --noproxy '*' -s --max-time 15 -X POST "$API/User/login" -H "$J" \
  -d '{"UserName":"wzm","Password":"123456"}')
TOKEN=$(echo "$LOGIN" | python3 -c "import sys,json;print(json.load(sys.stdin).get('data',{}).get('Token',''))" 2>/dev/null)
if [ -n "$TOKEN" ]; then ok "登录成功，Token 长度 ${#TOKEN}"; else no "登录失败: $LOGIN"; exit 1; fi
AUTH="Authorization: Bearer $TOKEN"

post(){ curl --noproxy '*' -s --max-time 60 -X POST "$API/$1" -H "$J" -H "$AUTH" -d "$2"; }
jq(){ python3 -c "
import sys,json
try: d=json.load(sys.stdin)
except Exception as e: print('PARSE_ERR',e); sys.exit()
$1" ; }

# ── 1. EntityConfig ──
echo "── 1. 列表配置（EntityConfig 是否加载） ──"
CFG=$(curl --noproxy '*' -s --max-time 20 "$API/Auditor/ExpertTask/config" -H "$AUTH")
echo "$CFG" | jq "
cols=d.get('data',{}).get('Columns') or d.get('data',{}).get('columns') or []
print(f\"  success={d.get('success')} 列数={len(cols)}\")
print('  列名:', ','.join([c.get('Field') or c.get('field') or '?' for c in cols][:20]))
print('  ✗ 零列 = EntityConfig 未加载' if len(cols)==0 else '  ✓ 列已加载')
" 

# ── 2. filter ──
echo "── 2. 任务列表 ──"
LST=$(post "Auditor/ExpertTask/filter" '{"Page":1,"PageSize":10,"Sorts":[{"Field":"CreateTime","Order":"desc"}]}')
echo "$LST" | jq "
d=d.get('data') or {}
items=d.get('Items') or d.get('items') or []
print(f\"  success={d.get('success')} total={d.get('Total')} 返回={len(items)}\")
for t in items[:3]:
    print(f\"   · {t.get('TaskNumber')} {t.get('TaskName')} [{t.get('TaskType')}] {t.get('ExecStatus')}/{t.get('ExecStatusLabel')} CanSubmit={t.get('CanSubmit')} Blocking={t.get('IsBlocking')}\")
"

# ── 3. lock-check ──
echo "── 3. 业务锁判定（应 CanCreate=true, CandidateCount=2） ──"
LC=$(post "Auditor/ExpertTask/lock-check" "{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"TaskType\":\"NC_CHECK\"}")
echo "$LC" | jq "
d=d.get('data') or {}
print(f\"  CanCreate={d.get('CanCreate')} CandidateCount={d.get('CandidateCount')} Existing={d.get('ExistingTaskCount')} AvgRound={d.get('AvgRoundCount')}\")
print('  Message:', d.get('Message'))
print('  ✓' if d.get('CanCreate') else '  ✗ 期望可创建')
"

# ── 4. candidates ──
echo "── 4. 候选检查项 ──"
CANDS=$(post "Auditor/ExpertTask/candidates" "{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"TaskType\":\"NC_CHECK\",\"StandardCodes\":[\"$STD\"]}")
echo "$CANDS" | jq "
d=d.get('data') or []
print(f\"  success={d.get('success') if isinstance(d,dict) else 'n/a'} 候选数={len(d) if isinstance(d,list) else 0}\")
for c in (d if isinstance(d,list) else [])[:5]:
    print(f\"   · {c.get('ItemNumber')} {c.get('ItemName')} Judge={c.get('JudgeMode')} HasWorkflow={c.get('HasWorkflow')} Suggested={c.get('Suggested')}\")
"

# ── 5. create ──
echo "── 5. 创建任务 ──"
CR=$(post "Auditor/ExpertTask/create" "{\"TaskName\":\"S4冒烟-复审NC\",\"TaskType\":\"NC_CHECK\",\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"ScopeType\":\"FULL\",\"TaskSource\":\"NEW\",\"StandardCodes\":[\"$STD\"],\"Remark\":\"S4自测\"}")
echo "$CR" | jq "
d=d.get('data') or {}
print(f\"  success={d.get('success')} err={d.get('err')}\")
print(f\"  TaskCode={d.get('TaskCode')} TaskNumber={d.get('TaskNumber')} Standard={d.get('StandardCount')} Item={d.get('ItemCount')} Queue={d.get('QueueCount')}\")
print('  Summary:', d.get('Summary'))
"
TASK=$(echo "$CR" | python3 -c "import sys,json;print((json.load(sys.stdin).get('data') or {}).get('TaskCode',''))" 2>/dev/null)

if [ -z "$TASK" ]; then
  echo "  ⚠ 创建未返回 TaskCode，后续步骤可能失败（可能是锁已存在）"
  # 尝试复用已有任务
  TASK=$(echo "$LST" | python3 -c "
import sys,json
d=json.load(sys.stdin).get('data') or {}
items=d.get('Items') or d.get('items') or []
print(items[0].get('Code','') if items else '')" 2>/dev/null)
  echo "  → 回退使用已有任务: $TASK"
fi

# ── 6. lock-check again（应被拒） ──
echo "── 6. 重复锁判定（应 CanCreate=false + 告知挡着的任务） ──"
LC2=$(post "Auditor/ExpertTask/lock-check" "{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"TaskType\":\"NC_CHECK\"}")
echo "$LC2" | jq "
d=d.get('data') or {}
print(f\"  CanCreate={d.get('CanCreate')} Blocking={d.get('BlockingTaskNumber')} {d.get('BlockingTaskName')} ({d.get('BlockingExecStatus')})\")
print('  Message:', d.get('Message'))
"

# ── 7. create again（应被拒） ──
echo "── 7. 重复创建（应被拒） ──"
CR2=$(post "Auditor/ExpertTask/create" "{\"TaskName\":\"S4冒烟-重复\",\"TaskType\":\"NC_CHECK\",\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"ScopeType\":\"FULL\",\"TaskSource\":\"NEW\",\"StandardCodes\":[\"$STD\"]}")
echo "$CR2" | jq "print('  success=',d.get('success'),' err=',d.get('err'))"

# ── 8. submit ──
echo "── 8. 提交执行 ──"
SB=$(post "Auditor/ExpertTask/submit" "{\"TaskCode\":\"$TASK\"}")
echo "$SB" | jq "print('  success=',d.get('success'),' msg=',d.get('message'),' err=',d.get('err'))"

# ── 9. detail ──
echo "── 9. 任务详情 ──"
DT=$(post "Auditor/ExpertTask/detail" "{\"TaskCode\":\"$TASK\"}")
echo "$DT" | jq "
d=d.get('data') or {}
t=d.get('Task') or {}
print(f\"  {t.get('TaskNumber')} {t.get('TaskName')} Exec={t.get('ExecStatus')} Life={t.get('LifecycleStatus')} 进度={t.get('Progress')}\")
print(f\"  企业={d.get('EnterpriseName')} 阶段={d.get('StageName')} 待审批={d.get('PendingReviewCount')}/{d.get('TotalResultCount')}\")
for q in (d.get('Queues') or []):
    print(f\"   queue {q.get('Code')} {q.get('QueueStatus')} {q.get('DoneCount')}/{q.get('TotalCount')} CanStart={q.get('CanStart')} CanPause={q.get('CanPause')}\")
    break
"

# ── 10. queue/start ──
QCODE=$(echo "$DT" | python3 -c "
import sys,json
d=json.load(sys.stdin).get('data') or {}
qs=d.get('Queues') or []
print(qs[0].get('Code','') if qs else '')" 2>/dev/null)
if [ -n "$QCODE" ]; then
  echo "── 10. 启动队列 $QCODE ──"
  QS=$(post "Auditor/ExpertTask/queue/start" "{\"QueueCode\":\"$QCODE\"}")
  echo "$QS" | jq "print('  success=',d.get('success'),' msg=',d.get('message'),' err=',d.get('err'))"
  echo "  （等待执行器跑完…）"
  sleep 12
else
  echo "── 10. 无队列，跳过 ──"
fi

# ── 11. 结果树 ──
echo "── 11. 结果树（Full=true，3 级） ──"
TR=$(post "Auditor/ExpertResult/tree" '{"Type":"nc","Full":true}')
echo "$TR" | python3 -c "
import sys,json
d=json.load(sys.stdin)
nodes=d.get('data') or []
print(f\"  success={d.get('success')} 根节点数={len(nodes)}\")
def walk(ns,lv):
    for n in ns:
        print('   '+'  '*lv+f\"[{n.get('NodeType')}] {n.get('Name')} total={n.get('TotalCount')} pending={n.get('PendingCount')}\")
        walk(n.get('Children') or [], lv+1)
walk(nodes,0)
"

# ── 12. 结果列表（空 TaskCode 应为空） ──
echo "── 12. 结果列表 TaskCode 为空（应空且不发全量） ──"
RL0=$(post "Auditor/ExpertResult/list" '{"Type":"nc","TaskCode":"","Page":1,"PageSize":20}')
echo "$RL0" | jq "
d=d.get('data') or {}
print(f\"  Rows={len(d.get('Rows') or [])} Total={d.get('Total')}\")
print('  ✓ 符合「必须选到任务」' if d.get('Total')==0 else '  ✗ 期望空')
"

echo "── 13. 结果列表（选中任务） ──"
RL=$(post "Auditor/ExpertResult/list" "{\"Type\":\"nc\",\"TaskCode\":\"$TASK\",\"Page\":1,\"PageSize\":50}")
echo "$RL" | jq "
d=d.get('data') or {}
rows=d.get('Rows') or []
print(f\"  Total={d.get('Total')} 返回={len(rows)}\")
for r in rows[:8]:
    print(f\"   · {r.get('ClauseNumber')} {r.get('ItemName')} 自动={r.get('AutoStatus')} 跳过={r.get('SkipCategory')} 结论={r.get('ConclusionLabel')} 来源={r.get('SourceLabel')} 复核={r.get('ReviewStatus')}\")
"
echo "$RL" | python3 -c "
import sys,json
d=json.load(sys.stdin).get('data') or {}
rows=d.get('Rows') or []
open('/tmp/s4_rows.json','w').write(json.dumps(rows,ensure_ascii=False))
print('  已保存',len(rows),'行 → /tmp/s4_rows.json')
"

# ── 13b. 勾选审批 ──
CODES=$(python3 -c "
import json
rows=json.load(open('/tmp/s4_rows.json'))
print(','.join([r['Code'] for r in rows if r.get('Code')]))")
if [ -n "$CODES" ]; then
  echo "── 13b. 批量认可（勾选审批） ──"
  ACKJSON=$(python3 -c "
import json
rows=json.load(open('/tmp/s4_rows.json'))
codes=[r['Code'] for r in rows if r.get('Code')]
print(json.dumps({'Type':'nc','Codes':codes,'Remark':'S4冒烟-认可'},ensure_ascii=False))")
  ACK=$(post "Auditor/ExpertResult/acknowledge" "$ACKJSON")
  echo "$ACK" | jq "print('  success=',d.get('success'),' msg=',d.get('message'),' err=',d.get('err'),' affected=',(d.get('data') or {}).get('Affected'))"

  echo "── 13c. 单行修改 ──"
  FIRST=$(python3 -c "
import json
rows=json.load(open('/tmp/s4_rows.json'))
print(next((r['Code'] for r in rows if r.get('Code')),''))")
  MODJSON=$(python3 -c "
import json
print(json.dumps({'Type':'nc','Code':'$FIRST','Conformity':'nonconform','Severity':'minor','ContentText':'S4冒烟：专家修改的不符合描述','EvidenceRef':'记录编号 R-001','ReviewRemark':'冒烟修改'},ensure_ascii=False))")
  MOD=$(post "Auditor/ExpertResult/modify" "$MODJSON")
  echo "$MOD" | jq "print('  success=',d.get('success'),' msg=',d.get('message'),' err=',d.get('err'))"

  echo "── 13d. 历史轮次 ──"
  ITEM=$(python3 -c "
import json
rows=json.load(open('/tmp/s4_rows.json'))
print(rows[0].get('ItemCode',''))")
  HIS=$(post "Auditor/ExpertResult/history" "{\"Type\":\"nc\",\"ItemCode\":\"$ITEM\"}")
  echo "$HIS" | python3 -c "
import sys,json
d=json.load(sys.stdin)
rows=d.get('data') or []
print(f\"  success={d.get('success')} 轮次数={len(rows)}\")
for r in rows[:5]:
    print(f\"   · 第{r.get('RoundNo')}轮 auto={r.get('AutoStatus')} 判定={r.get('Conformity')} 复核={r.get('ReviewStatus')} 改过={r.get('IsModified')}\")
"

  echo "── 13e. 修改后的列表（验证结论来源=人工修改） ──"
  RL2=$(post "Auditor/ExpertResult/list" "{\"Type\":\"nc\",\"TaskCode\":\"$TASK\",\"Page\":1,\"PageSize\":50}")
  echo "$RL2" | jq "
d=d.get('data') or {}
for r in (d.get('Rows') or []):
    print(f\"   · {r.get('ClauseNumber')} {r.get('ItemName')} 结论={r.get('ConclusionLabel')} 严重度={r.get('Severity')} 复核={r.get('ReviewStatus')} 来源={r.get('SourceLabel')}\")
"
else
  echo "── 13b~13e. 无结果行，跳过审批/修改/历史 ──"
fi

echo "── 14. 导出 CSV ──"
curl --noproxy '*' -s --max-time 30 -D /tmp/s4_hdr.txt -o /tmp/s4_export.csv -X POST \
  "$API/Auditor/ExpertResult/export" -H "$J" -H "$AUTH" \
  -d "{\"Type\":\"nc\",\"TaskCode\":\"$TASK\"}"
echo "  Content-Type: $(grep -i '^content-type' /tmp/s4_hdr.txt | tr -d '\r')"
echo "  文件头(hex): $(head -c 3 /tmp/s4_export.csv | xxd -p)"
echo "  行数: $(wc -l < /tmp/s4_export.csv)"
head -4 /tmp/s4_export.csv | sed 's/^/   | /'

# ── 15. 日志 ──
echo "── 15. 运行日志 ──"
LG=$(post "Auditor/ExpertTask/logs" "{\"TaskCode\":\"$TASK\",\"Take\":50}")
echo "$LG" | jq "
d=d.get('data') or []
print(f\"  日志数={len(d) if isinstance(d,list) else 0}\")
for l in (d if isinstance(d,list) else [])[:12]:
    print(f\"   · {l.get('OperateTime','')[:19]} [{l.get('LogLevel')}] {l.get('LogAction')} {l.get('ItemName') or ''} {l.get('Message') or ''}\")
"

# ── 16. 缺口 ──
echo "── 16. 数据缺口 ──"
GP=$(post "Auditor/ExpertTask/gaps" "{\"TaskCode\":\"$TASK\"}")
echo "$GP" | jq "d=d.get('data') or [];print(f\"  缺口数={len(d) if isinstance(d,list) else 0}\")"

echo ""
echo "════════════════════════════════════════"
echo "冒烟脚本结束，硬失败数: $FAIL"
echo "TASK=$TASK"
echo "$TASK" > /tmp/s4_task_code.txt

# ── 17. 数据库侧核对 ──
echo ""
echo "── 17. 数据库核对 ──"
docker exec yzh-mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" --default-character-set=utf8mb4 -t -B yzh_cert_platform -e "
SELECT ExecStatus, LifecycleStatus, TotalItemCount, AckedCount, ModifiedCount,
       IFNULL(ActiveLockKey,'<NULL=已解锁>') AS LockKey, Progress
FROM cert_expert_task WHERE Code='$TASK';
SELECT ItemStatus, COUNT(*) AS n FROM cert_expert_task_queue_item WHERE TaskCode='$TASK' GROUP BY ItemStatus;
SELECT AutoStatus, SkipCategory, Conformity, Severity, ReviewStatus, IsModified
FROM cert_expert_nc_result WHERE TaskCode='$TASK' ORDER BY RoundNo;
SELECT QueueStatus, TotalCount, DoneCount, SkippedCount, FailedCount, Progress
FROM cert_expert_task_queue WHERE TaskCode='$TASK';
SELECT ClauseNumber, ClauseTitle, JudgeMode FROM cert_expert_nc_item LIMIT 3;
SELECT PhaseCode, TaskStatus, ErrorMessage FROM wf_execution_task ORDER BY Id DESC LIMIT 3;
" 2>/dev/null
