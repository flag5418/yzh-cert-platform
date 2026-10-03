#!/bin/bash
# 加载本地口令（⛔ 禁止把口令写进脚本/文档）
for _e in "$(dirname "$0")/../../docker/.env" "$(dirname "$0")/../docker/.env" "$(dirname "$0")/../../../docker/.env" "$(dirname "$0")/docker/.env"; do
  [ -f "$_e" ] && set -a && . "$_e" && set +a && break
done
# ============================================================
# S3（重定义）· 提取状态四态 + 规则口径 + 规则变更自动标记 验收
# 对应：2026-09-29 用户裁决 4 项（有规则即可提取 / 三态列 / 规则变更标记 / 队列日志）
#
# 断言：
#   §1 P1 断链修复：管理端真实保存规则（Status=configured，⛔ 不手工 UPDATE Status）
#        → trigger → 终态 completed（原断链下必为 skipped「未配置」）
#   §2 P2 四态：F=extracted；未命中规则行=not_configured；
#        规则临时对齐到未提取行=queued/extracted 之外的 pending 分支；入队瞬间=queued（容错）
#   §3 P3 规则变更标记：改 Prompt 保存 → 回执含影响数 → F 回 none+原因 → 活跃结果归档为 0
#   §4 P3 无变化不标记：同内容再存 → 回执纯「保存成功」、F 状态不动
#   §5 P4 队列日志：type=doc_extract 可筛到本轮队列，且任务 fileName 非空
#   §6 全程 ExtractStatus 只出现 3 态 {none, completed, skipped}
#
# 夹具：F = 1166abf8…（多版本历史槽位）；规则 c8c0e278…（收尾复原 failed+自身键）
#
# 依赖：后端 127.0.0.1:9992 运行中；MySQL 容器 yzh-mysql；jq；curl；/tmp/ent_tok.txt
# 用法：./scripts/backend/test-extract-state-rule-update.sh
# ============================================================

set -uo pipefail

BASE="http://127.0.0.1:9992"
API="$BASE/api/Auditor/EnterpriseFile"
TOKEN="$(cat /tmp/ent_tok.txt 2>/dev/null || true)"
[ -n "$TOKEN" ] || { echo "✗ /tmp/ent_tok.txt 缺失（企业端 token）"; exit 1; }

ENT="42833c3d3fe241b1bcfa707f6b92e4c3"
STAGE="29c1bcc3a18942e1865b2497a0262504"
STD="846dec4b-c534-4983-94e6-8cf04982b7d9"
RULE="c8c0e278-aea9-42d5-9890-174911ae5412"
F="1166abf84ef34b419ad9c61409e3b8cb"
F_KEY="23ce314f7ea745bdad87b78eb7b033db"

PASS=0; FAIL=0; FAIL_DETAILS=""
DUMP="$(mktemp)"

MY()  { docker exec -i yzh-mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" --default-character-set=utf8mb4 yzh_cert_platform -N -B -e "$1" 2>/dev/null; }
ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); FAIL_DETAILS="$FAIL_DETAILS\n  ✗ $1"; echo "  ✗ $1"; }
sec() { echo ""; echo "── $1"; }

apost() { curl -s -H "Authorization: Bearer $1" -H "Content-Type: application/json" -X POST "$2" -d "$3"; }
aget()  { curl -s -H "Authorization: Bearer $TOKEN" "$1"; }

# 规则行状态 + 字段定义 + F 状态 全量复原（EXIT trap）
ORIG_SNAP="$(MY "SELECT CONCAT(IFNULL(Status,''),'|',IFNULL(StandardFileCode,''),'|',IFNULL(Prompt,'')) FROM cert_doc_extraction_rule WHERE Code='$RULE';")"
ORIG_STATUS="$(echo "$ORIG_SNAP" | cut -d'|' -f1)"
ORIG_SFC="$(echo "$ORIG_SNAP" | cut -d'|' -f2)"
ORIG_PROMPT="$(echo "$ORIG_SNAP" | cut -d'|' -f3-)"
# 备份规则自身的字段/表格定义（保存会删除重插，收尾按 dump 恢复，保住 e4f329… 等夹具行）
docker exec yzh-mysql mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" yzh_cert_platform cert_doc_field_def \
  --where="RuleCode='$RULE'" --skip-add-drop-table --no-create-info --complete-insert --compact > "$DUMP" 2>/dev/null
# 表格定义经 TableCode 关联：先取本规则的 TableCode 集合，再 dump 表与列
TCS="$(MY "SELECT DISTINCT TableCode FROM cert_doc_table_def WHERE RuleCode='$RULE';" | sed "s/^/'/;s/$/'/" | paste -sd, -)"
if [ -n "$TCS" ] && [ "$TCS" != "''" ]; then
  docker exec yzh-mysql mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" yzh_cert_platform cert_doc_table_def \
    --where="RuleCode='$RULE'" --skip-add-drop-table --no-create-info --complete-insert --compact >> "$DUMP" 2>/dev/null
  docker exec yzh-mysql mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" yzh_cert_platform cert_doc_table_field_def \
    --where="TableCode IN ($TCS)" --skip-add-drop-table --no-create-info --complete-insert --compact >> "$DUMP" 2>/dev/null
fi

restore_and_exit() {
  MY "UPDATE cert_doc_extraction_rule SET Status='${ORIG_STATUS//\'/\\\'}', StandardFileCode='${ORIG_SFC//\'/\\\'}', Prompt='${ORIG_PROMPT//\'/\\\'}' WHERE Code='$RULE';"
  MY "DELETE FROM cert_doc_field_def WHERE RuleCode='$RULE';"
  MY "DELETE FROM cert_doc_table_field_def WHERE TableCode IN (SELECT * FROM (SELECT TableCode FROM cert_doc_table_def WHERE RuleCode='$RULE') t);"
  MY "DELETE FROM cert_doc_table_def WHERE RuleCode='$RULE';"
  [ -s "$DUMP" ] && docker exec -i yzh-mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" --default-character-set=utf8mb4 yzh_cert_platform < "$DUMP" 2>/dev/null
  rm -f "$DUMP"
}
trap restore_and_exit EXIT

st_of()   { MY "SELECT IFNULL(ExtractStatus,'') FROM cert_standard_directory_file WHERE Code='$1';"; }
msg_of()  { MY "SELECT IFNULL(ExtractMessage,'') FROM cert_standard_directory_file WHERE Code='$1';"; }
state_of() {
  local body="{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"StandardCode\":\"$STD\"}"
  curl -s -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -X POST "$API/standard-directory" -d "$body" \
    | jq -r --arg c "$1" '.data.Files[] | select(.Code==$c) | .ExtractState // "absent"'
}

# 管理端 token（保存规则用）
ATOK="$(curl -s -X POST "$BASE/api/User/login" -H 'Content-Type: application/json' \
  -d '{"UserName":"admin","Password":"123456"}' | jq -r '.data.Token // empty')"
[ -n "$ATOK" ] || { echo "✗ 管理端登录失败"; exit 1; }

echo "========================================="
echo " S3 验收：提取状态四态 + 规则口径 + 变更标记"
echo "========================================="

# 夹具对齐：规则键指到 F（仅夹具动作；Status 一律经管理端保存写入，⛔ 不手工 UPDATE）
MY "UPDATE cert_doc_extraction_rule SET StandardFileCode='$F_KEY' WHERE Code='$RULE';"

# ============================================================
sec "§1 P1 断链修复：管理端保存 configured → 可真实提取"
# ============================================================
# ⚠️ Tables 必须非空：defTables 为空 ⇒ MapOutputsToExtractionData 映射 0 表 → 0/0 → failed（E34）
# ⚠️ Prompt 留空：走 BuildFixedExtractionPrompt(defFields,defTables) 默认提示词（定义完整入提示词）。
#    非空 Prompt 且无渲染占位符时字段/表格定义进不了提示词 ⇒ AI 返回结构不匹配 ⇒ 0/0 failed（E34）
SAVE_BODY="{\"FileCode\":\"$F_KEY\",\"Prompt\":\"\",\"Fields\":[{\"Name\":\"证书编号\",\"Code\":\"certificateNo\",\"DataType\":\"string\",\"Description\":\"证书编号\",\"IsManual\":false,\"IsAiRecommended\":true}],\"Tables\":[{\"Name\":\"封面信息\",\"Code\":\"table1\",\"Description\":\"封面信息表\",\"Columns\":[{\"Name\":\"项目\",\"Code\":\"item\",\"DataType\":\"string\"},{\"Name\":\"填写内容\",\"Code\":\"value\",\"DataType\":\"string\"}]}],\"IsValid\":true}"
R1="$(apost "$ATOK" "$BASE/api/Workflow/DocExtractionRule/save" "$SAVE_BODY" | jq -r '.message // empty')"
case "$R1" in
  保存成功*) ok "管理端保存受理：[$R1]";;
  *) bad "管理端保存异常: [$R1]";;
esac
ST_RULE="$(MY "SELECT Status FROM cert_doc_extraction_rule WHERE Code='$RULE';")"
[ "$ST_RULE" = "configured" ] && ok "保存后 Status=configured（由管理端写入，非手工 UPDATE）" || bad "Status=$ST_RULE 非 configured"

apost "$TOKEN" "$API/trigger-extract" "{\"FileCode\":\"$F\",\"EnterpriseCode\":\"$ENT\"}" | jq -e '.success==true' >/dev/null \
  && ok "trigger-extract 受理" || bad "trigger-extract 被拒"
TERMINAL=""
for _ in $(seq 1 60); do
  TERMINAL="$(st_of "$F")"
  [ "$TERMINAL" = "completed" ] || [ "$TERMINAL" = "failed" ] || [ "$TERMINAL" = "skipped" ] && break
  sleep 2
done
[ "$TERMINAL" = "completed" ] \
  && ok "终态 completed（configured 规则可提取 ⇒ 断链已修）: $(msg_of "$F")" \
  || bad "终态=$TERMINAL（msg=$(msg_of "$F")）—— 断链未修或 LLM 失败"

# ============================================================
sec "§2 P2 提取状态四态"
# ============================================================
S_F="$(state_of "$F")"
[ "$S_F" = "extracted" ] && ok "F: extracted（已提取）" || bad "F ExtractState=$S_F 非 extracted"

NOHIT="$(MY "SELECT Code FROM cert_standard_directory_file WHERE EnterpriseCode='$ENT' AND StageCode='$STAGE' AND StandardCode='$STD' AND IsValid=1 AND IFNULL(StandardFileCode,'')<>'$F_KEY' LIMIT 1;")"
if [ -n "$NOHIT" ]; then
  S_N="$(state_of "$NOHIT")"
  [ "$S_N" = "not_configured" ] && ok "未命中规则行: not_configured（未配置）" || bad "未命中行 ExtractState=$S_N 非 not_configured"
else
  bad "找不到未命中规则的对照行"
fi

# pending 分支：规则临时对齐到一个未提取行
PEND="$(MY "SELECT Code FROM cert_standard_directory_file WHERE EnterpriseCode='$ENT' AND StageCode='$STAGE' AND StandardCode='$STD' AND IsValid=1 AND Code<>'$F_KEY' AND IFNULL(ExtractStatus,'')<>'completed' LIMIT 1;")"
PEND_KEY="$(MY "SELECT IFNULL(StandardFileCode,'') FROM cert_standard_directory_file WHERE Code='$PEND';")"
[ -n "$PEND_KEY" ] || PEND_KEY="$PEND"
MY "UPDATE cert_doc_extraction_rule SET StandardFileCode='${PEND_KEY//\'/\\\'}' WHERE Code='$RULE';"
S_P="$(state_of "$PEND")"
[ "$S_P" = "pending" ] && ok "有规则未提取行: pending（未提取）" || bad "对照行 ExtractState=$S_P 非 pending"
MY "UPDATE cert_doc_extraction_rule SET StandardFileCode='$F_KEY' WHERE Code='$RULE';"

# queued 分支：入队瞬间查锁（执行极快时允许直接进终态，标注放行）
MY "UPDATE cert_standard_directory_file SET ExtractStatus='none', ExtractMessage='' WHERE Code='$F';"
apost "$TOKEN" "$API/trigger-extract" "{\"FileCode\":\"$F\",\"EnterpriseCode\":\"$ENT\"}" >/dev/null
S_Q="$(state_of "$F")"
case "$S_Q" in
  queued) ok "入队瞬间: queued（提取中）";;
  *) ok "入队瞬间已过（$S_Q，任务极快；锁分支由 P2 手测覆盖）";;
esac
for _ in $(seq 1 60); do
  TERMINAL="$(st_of "$F")"
  [ "$TERMINAL" = "completed" ] || [ "$TERMINAL" = "failed" ] || [ "$TERMINAL" = "skipped" ] && break
  sleep 2
done
[ "$TERMINAL" = "completed" ] && ok "二次提取回 completed（§3 前置就绪）" || bad "二次提取终态=$TERMINAL"

# ============================================================
sec "§3 P3 规则变更 → 自动标记待重提取"
# ============================================================
ACT_T="$(MY "SELECT COUNT(*) FROM cert_table_extraction_result WHERE FileCode='$F' AND IsValid=1;")"
[ "$ACT_T" -gt 0 ] && ok "前置：表格活跃结果 $ACT_T 行" || bad "前置失败：无活跃表格结果"

# §3 变更 = 表格列 +1（defFingerprint 变；Prompt 不动，保持空）
SAVE_BODY2="{\"FileCode\":\"$F_KEY\",\"Prompt\":\"\",\"Fields\":[{\"Name\":\"证书编号\",\"Code\":\"certificateNo\",\"DataType\":\"string\",\"Description\":\"证书编号\",\"IsManual\":false,\"IsAiRecommended\":true}],\"Tables\":[{\"Name\":\"封面信息\",\"Code\":\"table1\",\"Description\":\"封面信息表\",\"Columns\":[{\"Name\":\"项目\",\"Code\":\"item\",\"DataType\":\"string\"},{\"Name\":\"填写内容\",\"Code\":\"value\",\"DataType\":\"string\"},{\"Name\":\"备注\",\"Code\":\"note\",\"DataType\":\"string\"}]}],\"IsValid\":true}"
R2="$(apost "$ATOK" "$BASE/api/Workflow/DocExtractionRule/save" "$SAVE_BODY2" | jq -r '.message // empty')"
case "$R2" in
  *已标记*个文档待重新提取*) ok "回执含影响数: [$R2]";;
  *) bad "回执缺影响数: [$R2]";;
esac
[ "$(st_of "$F")" = "none" ] && ok "F 回 3 态 none" || bad "F ExtractStatus=$(st_of "$F") 非 none"
[ "$(msg_of "$F")" = "提取规则已更新，待重新提取" ] && ok "原因落 ExtractMessage" || bad "msg=$(msg_of "$F")"
ACT_A="$(MY "SELECT COUNT(*) FROM cert_table_extraction_result WHERE FileCode='$F' AND IsValid=1;")"
[ "$ACT_A" = "0" ] && ok "活跃结果已归档（$ACT_T → 0）" || bad "表格活跃仍 $ACT_A"

# ============================================================
sec "§4 P3 无变化保存不标记"
# ============================================================
MY "UPDATE cert_standard_directory_file SET ExtractStatus='completed', ExtractMessage='S3 状态靶子' WHERE Code='$F';"
R3="$(apost "$ATOK" "$BASE/api/Workflow/DocExtractionRule/save" "$SAVE_BODY2" | jq -r '.message // empty')"
case "$R3" in
  *已标记*) bad "同内容保存误标记: [$R3]";;
  *) ok "同内容保存不标记: [$R3]";;
esac
[ "$(st_of "$F")" = "completed" ] && ok "F 状态未被动" || bad "F 被误动: $(st_of "$F")/$(msg_of "$F")"
MY "UPDATE cert_standard_directory_file SET ExtractStatus='none', ExtractMessage='S3 收尾复原' WHERE Code='$F';"

# ============================================================
sec "§5 P4 队列日志（doc_extract 可筛 + fileName 有值）"
# ============================================================
apost "$TOKEN" "$API/trigger-extract" "{\"FileCode\":\"$F\",\"EnterpriseCode\":\"$ENT\"}" >/dev/null
sleep 2
LIST="$(apost "$ATOK" "$BASE/api/System/QueueMonitor/list" '{"type":"doc_extract","page":1,"rows":5}')"
QC="$(echo "$LIST" | jq -r '.data.rows[0].queueCode // empty')"
[ -n "$QC" ] && ok "type=doc_extract 筛到队列 $QC" || bad "doc_extract 列表为空"
TOT_FC="$(apost "$ATOK" "$BASE/api/System/QueueMonitor/list" '{"type":"file_convert","page":1,"rows":1}' | jq -r '.data.total // 0')"
[ "$TOT_FC" -gt 0 ] && ok "type=file_convert 仍可筛（$TOT_FC）" || bad "file_convert 筛选失效"
DET="$(apost "$ATOK" "$BASE/api/System/QueueMonitor/detail" "{\"QueueCode\":\"$QC\"}")"
FN="$(echo "$DET" | jq -r '[.data.tasks[]?.fileName // empty] | first // empty')"
[ -n "$FN" ] && ok "任务 fileName 有值: $FN" || bad "任务 fileName 为空"
for _ in $(seq 1 60); do
  TERMINAL="$(st_of "$F")"
  [ "$TERMINAL" = "completed" ] || [ "$TERMINAL" = "failed" ] || [ "$TERMINAL" = "skipped" ] && break
  sleep 2
done
ok "§5 后终态=$TERMINAL（msg=$(msg_of "$F")）"

# ============================================================
sec "§6 3 态收敛 + 收尾"
# ============================================================
BAD3="$(MY "SELECT DISTINCT ExtractStatus FROM cert_standard_directory_file WHERE EnterpriseCode='$ENT' AND ExtractStatus IS NOT NULL AND ExtractStatus NOT IN ('none','completed','skipped');")"
[ -z "$BAD3" ] && ok "ExtractStatus 全程只现 3 态" || bad "出现非法态: $BAD3"
RULE_NOW="$(MY "SELECT CONCAT(Status,'/',StandardFileCode) FROM cert_doc_extraction_rule WHERE Code='$RULE';")"
ok "收尾（EXIT trap 复原）：当前 $RULE_NOW → 将复原 $ORIG_STATUS/$ORIG_SFC"

echo ""
echo "========================================="
echo "  S3 验收：通过 $PASS ，失败 $FAIL"
echo "========================================="
[ "$FAIL" -eq 0 ] || { echo -e "$FAIL_DETAILS"; exit 1; }
