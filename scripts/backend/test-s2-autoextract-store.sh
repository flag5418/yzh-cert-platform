#!/bin/bash
# ============================================================
# S2 · 自动提取开关 + 提取结果版本店 + 3 态收敛 验收
# 对应：docs/40-实施/企业资料管理/10-执行计划-V1.md §六 S2 验收①–⑥
#
# 断言：
#   ① 上传 AutoExtract=true  ⇒ 自动产出 doc_extract（extract_done 留痕），终态 ∈ {completed, skipped}
#   ② 上传 AutoExtract=false ⇒ 不入提取队列、ExtractStatus='none'、原因写 ExtractMessage
#   ③ 提取成功 → 替换 ⇒ 读取口径为空；换文件（AutoExtract=true）后自动重新提取
#   ④ 移除 ⇒ 读取口径为空
#   ⑤ 恢复到 v1 ⇒ v1 字段原样回来
#   ⑥ 全程 cert_extraction_result 行数不减（只翻转 IsValid）
#   附：全程 ExtractStatus 只出现 3 态 {none, completed, skipped}
#
# 夹具策略：
#   F  = 1166abf8…（历史槽位，已有多次替换）→ 承担 ③⑥（F）
#   FC1= 本轮新上传槽位（VersionNumber=1）    → 承担 ①④⑤（字面 v1）
#   FC2= 本轮新上传槽位                        → 承担 ②
#   规则只能指向一个规则键 ⇒ 按阶段切换 align_rule（收尾 restore 复原）
#
# 依赖：后端 127.0.0.1:9992 运行中；MySQL 容器 yzh-mysql；jq；curl；/tmp/ent_tok.txt
# 用法：./scripts/backend/test-s2-autoextract-store.sh
# ============================================================

set -uo pipefail

BASE="http://127.0.0.1:9992"
API="$BASE/api/Auditor/EnterpriseFile"     # ⛔ apost 必须用 $API，用 $BASE 会 404
TOKEN="$(cat /tmp/ent_tok.txt 2>/dev/null || true)"
[ -n "$TOKEN" ] || { echo "✗ /tmp/ent_tok.txt 缺失（企业端 token）"; exit 1; }

ENT="42833c3d3fe241b1bcfa707f6b92e4c3"
STAGE="29c1bcc3a18942e1865b2497a0262504"
STD="846dec4b-c534-4983-94e6-8cf04982b7d9"
RULE="c8c0e278-aea9-42d5-9890-174911ae5412"
RULE_KEY_ORIG="12711e0cc94e4d7c804ee82b9b1a683f"   # 规则自身模板行（收尾复原）
DEF_CODE="e4f329d319874a6db520612740c5fa79"
F="1166abf84ef34b419ad9c61409e3b8cb"                # 夹具 F：多版本历史槽位
F_KEY="23ce314f7ea745bdad87b78eb7b033db"            # F 的规则键

PASS=0; FAIL=0; FAIL_DETAILS=""
TMPDIR_S="$(mktemp -d)"

restore_and_exit() {
  MY "UPDATE cert_doc_extraction_rule SET Status='failed', StandardFileCode='$RULE_KEY_ORIG' WHERE Code='$RULE';"
  MY "UPDATE cert_doc_field_def SET FieldCode='certificateNo', FieldName='证书编号' WHERE Code='$DEF_CODE';"
}
trap restore_and_exit EXIT

MY() { docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 yzh_cert_platform -N -B -e "$1" 2>/dev/null; }
ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); FAIL_DETAILS="$FAIL_DETAILS\n  ✗ $1"; echo "  ✗ $1"; }
sec() { echo ""; echo "── $1"; }
now() { date '+%F %T'; }

apost() { curl -s -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -X POST "$API$1" -d "$2"; }
aget()  { curl -s -H "Authorization: Bearer $TOKEN" "$1"; }

st_of()   { MY "SELECT IFNULL(ExtractStatus,'') FROM cert_standard_directory_file WHERE Code='$1';"; }
msg_of()  { MY "SELECT IFNULL(ExtractMessage,'') FROM cert_standard_directory_file WHERE Code='$1';"; }
conv_of() { MY "SELECT IFNULL(ConvertStatus,'') FROM cert_standard_directory_file WHERE Code='$1';"; }
rule_key_of() { local k; k=$(MY "SELECT IFNULL(StandardFileCode,'') FROM cert_standard_directory_file WHERE Code='$1';"); [ -n "$k" ] && echo "$k" || echo "$1"; }
extract_done_after() { MY "SELECT COUNT(*) FROM cert_enterprise_file_op_log WHERE FileCode='$1' AND OpType='extract_done' AND CreateTime >= '$2' AND IsValid=1;"; }
active_rows() { MY "SELECT COUNT(*) FROM cert_extraction_result WHERE OrgCode='$ENT' AND FileCode='$1' AND IsValid=1;"; }
all_rows()    { MY "SELECT COUNT(*) FROM cert_extraction_result WHERE OrgCode='$ENT' AND FileCode='$1';"; }
result_fields() { aget "$API/extraction-result/$1?enterpriseCode=$ENT" | jq -r 'if .data then (.data.Fields|length) else 0 end'; }
align_rule() { MY "UPDATE cert_doc_extraction_rule SET Status='passed', StandardFileCode='$1' WHERE Code='$RULE';"; }

# ⚠️ macOS bash 3.2：`[ "$(f "{...\":...}")" = x ]` 会把 JSON 当成花括号展开并拆词
# （实测 A2=["ConfigCode":"..."]、apost 被调 2 次）⇒ payload 必须先落变量，再进 [ ] 的命令替换。
wait_idle() {
  local cfg="$1" deadline=$(($(date +%s)+$2)) payload busy
  payload="{\"ConfigCode\":\"$cfg\",\"EnterpriseCode\":\"$ENT\"}"
  while [ "$(date +%s)" -lt "$deadline" ]; do
    busy="$(apost "/active-queue" "$payload" | jq -r '.data.IsBusy')"
    [ "$busy" = "false" ] && return 0
    sleep 2
  done
  return 1
}
wait_extract_done() { # file since timeout
  local deadline=$(($(date +%s)+$3))
  while [ "$(date +%s)" -lt "$deadline" ]; do
    [ "$(extract_done_after "$1" "$2")" -gt 0 ] && return 0
    sleep 2
  done
  return 1
}
wait_convert_done() { # file timeout
  local deadline=$(($(date +%s)+$2))
  while [ "$(date +%s)" -lt "$deadline" ]; do
    [ "$(conv_of "$1")" = "completed" ] && return 0
    sleep 2
  done
  return 1
}
assert3state() { # file phase
  local s; s=$(st_of "$1")
  case "$s" in none|completed|skipped) ok "$2 3 态合法（$s）" ;; *) bad "$2 出现 3 态外取值: '$s'" ;; esac
}
upload_to_slot() { # slotcode filename autoextract -> sets TID/FCN
  local slot="$1" fname="$2" auto="$3"
  local init; init=$(apost "/upload/init" "{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"StandardCode\":\"$STD\",\"AutoExtract\":$auto,\"Items\":[{\"SlotCode\":\"$slot\",\"FileName\":\"$fname\",\"FileSize\":10}]}")
  TID=$(echo "$init" | jq -r '.data.TaskId // empty'); FCN=$(echo "$init" | jq -r '.data.Items[0].FileCode // empty')
  [ -n "$FCN" ] || return 1
  curl -s -H "Authorization: Bearer $TOKEN" -X POST "$API/upload/file" \
       -F "TaskId=$TID" -F "FileCode=$FCN" -F "File=@$TMPDIR_S/sample.docx;filename=$fname" \
    | jq -e '.success==true' >/dev/null || return 1
  return 0
}
replace_file() { # filecode autoextract reason
  curl -s -H "Authorization: Bearer $TOKEN" -X POST "$API/replace" \
    -F "FileCode=$1" -F "EnterpriseCode=$ENT" -F "Reason=$3" -F "AutoExtract=$2" \
    -F "File=@$TMPDIR_S/sample.docx;filename=质量手册封面.docx"
}

# ============================================================
echo "=== 0. 预置：字段改为可提取项 + 规则对齐 F ==="
MY "UPDATE cert_doc_field_def SET FieldCode='projectName', FieldName='项目名称' WHERE Code='$DEF_CODE';"
align_rule "$F_KEY"
ok "字段定义 = 项目名称/projectName；规则对齐 F（Status=passed）"

SD=$(apost "/standard-directory" "{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"StandardCode\":\"$STD\"}" | jq -r '.data // empty')
CFG=$(echo "$SD" | jq -r '.EnterpriseConfigCode // empty')
[ -n "$CFG" ] && ok "标准目录已初始化（ConfigCode=$CFG）" || bad "标准目录未初始化（接口未通）"
wait_idle "$CFG" 60 || bad "初始队列未空闲"

curl -s -H "Authorization: Bearer $TOKEN" "$API/download?storagePath=$(python3 -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1],safe=''))" "$(MY "SELECT StoragePath FROM cert_standard_directory_file WHERE Code='$F';")")" \
  -o "$TMPDIR_S/sample.docx"
[ -s "$TMPDIR_S/sample.docx" ] && ok "下载样例原件 $(wc -c <"$TMPDIR_S/sample.docx") 字节" || bad "样例原件下载失败"

if [ -z "$(MY "SELECT IFNULL(MarkdownPath,'') FROM cert_standard_directory_file WHERE Code='$F';")" ] || [ "$(conv_of "$F")" != "completed" ]; then
  echo "  … F 产物缺失，先做一次 AutoExtract=false 替换重建"
  RPR=$(replace_file "$F" false "S2验收-重建产物")
  echo "$RPR" | jq -e '.success==true' >/dev/null && ok "F 重建替换受理" || bad "F 重建替换失败: $(echo "$RPR" | jq -r '.err')"
  wait_idle "$CFG" 240 || bad "F 重建转换队列超时"
  wait_convert_done "$F" 120 || bad "F 重建后 ConvertStatus=$(conv_of "$F")"
fi

# ============================================================
sec "1. 预置结果：手动触发 F 提取 → completed（写入路径 + 3 态）"
T0=$(now)
apost "/trigger-extract" "{\"FileCode\":\"$F\",\"EnterpriseCode\":\"$ENT\"}" | jq -e '.success==true' >/dev/null \
  && ok "trigger-extract 受理" || bad "trigger-extract 被拒"
wait_extract_done "$F" "$T0" 180 || bad "F 未产生 extract_done 留痕（超时）"
[ "$(st_of "$F")" = "completed" ] && ok "F 终态 completed" || bad "F 终态=$(st_of "$F")，msg=$(msg_of "$F")"
F_BASE=$(all_rows "$F")
[ "$(active_rows "$F")" -ge 1 ] && ok "F 活跃结果行 = $(active_rows "$F")" || bad "F 活跃结果行 = 0"
[ "$(result_fields "$F")" -ge 1 ] && ok "F 读取口径字段 = $(result_fields "$F")" || bad "F 读取口径字段 = 0"
assert3state "$F" "预置后"

# ============================================================
sec "2. ① 上传 AutoExtract=true ⇒ 自动入提取队列（FC1，VersionNumber=1）"
S1C=$(echo "$SD" | jq -r '[.Files[] | select(.Status=="missing")][0].Code')
S1N=$(echo "$SD" | jq -r '[.Files[] | select(.Status=="missing")][0].FileName')
if [ -z "$S1C" ] || [ "$S1C" = "null" ]; then
  bad "无缺失槽位可上传"
else
  T1=$(now)
  upload_to_slot "$S1C" "$S1N" true && ok "init+upload 成功（${S1N}）" || bad "init/upload 失败"
  if [ -n "${FCN:-}" ]; then
    FC1="$FCN"; FC1_KEY=$(rule_key_of "$FC1")
    align_rule "$FC1_KEY"   # 规则切到 FC1，确保①能走到 completed（夹具④⑤也要它的结果）
    ok "规则切到 FC1 规则键 ${FC1_KEY:0:8}…"
    CONF=$(apost "/upload/confirm" "{\"TaskId\":\"$TID\",\"EnterpriseCode\":\"$ENT\",\"AutoExtract\":true}")
    QC=$(echo "$CONF" | jq -r '.data.QueueCode // empty')
    [ -n "$QC" ] && ok "confirm 自动产出 file_convert 队列 $QC" || bad "confirm 未入转换队列: $(echo "$CONF" | jq -r '.err')"
    wait_extract_done "$FC1" "$T1" 300 || bad "① 未见 extract_done（转换→提取链未打通，超时）"
    ST1=$(st_of "$FC1")
    case "$ST1" in
      completed|skipped) ok "① 终态 = $ST1 ∈ {completed, skipped}" ;;
      *) bad "① 终态 = '$ST1'，msg=$(msg_of "$FC1")" ;;
    esac
    assert3state "$FC1" "① 上传后"
    FC1_BASE=$(all_rows "$FC1")
    if [ "$ST1" = "completed" ]; then
      [ "$(active_rows "$FC1")" -ge 1 ] && ok "① FC1 活跃行 = $(active_rows "$FC1")（供④⑤用）" || bad "① FC1 活跃行 = 0"
    fi

    # 夹具补强（⑤ 前置）：从未替换过的文件没有 EnterpriseFileVersion v1 归档件。
    # 再做一次 AutoExtract=true 替换 ⇒ 制造 v1 归档 + 结果版本切到 v2（④ 删除时口径非空才有意义）
    TB=$(now)
    R=$(replace_file "$FC1" true "S2验收-制造v1归档")
    echo "$R" | jq -e '.success==true' >/dev/null && ok "夹具：FC1 归档替换受理（造 v1 归档件）" || bad "夹具：FC1 归档替换失败: $(echo "$R" | jq -r '.err')"
    wait_extract_done "$FC1" "$TB" 300 || bad "夹具：FC1 归档替换后未自动提取（超时）"
    V1=$(MY "SELECT COUNT(*) FROM cert_enterprise_file_version WHERE FileCode='$FC1' AND VersionNumber=1 AND IsValid=1;")
    [ "$V1" -ge 1 ] && ok "夹具：v1 归档件已存在（$V1 条）" || bad "夹具：cert_enterprise_file_version 无 v1"
    FC1_BASE=$(all_rows "$FC1")
    [ "$(active_rows "$FC1")" -ge 1 ] && ok "夹具：FC1 活跃行 = $(active_rows "$FC1")（供④用）" || bad "夹具：FC1 活跃行 = 0"
    assert3state "$FC1" "夹具替换后"
  fi
fi

# ============================================================
sec "3. ② 上传 AutoExtract=false ⇒ 只转换不提取（FC2）"
SD2=$(apost "/standard-directory" "{\"EnterpriseCode\":\"$ENT\",\"StageCode\":\"$STAGE\",\"StandardCode\":\"$STD\"}" | jq -r '.data // empty')
S2C=$(echo "$SD2" | jq -r '[.Files[] | select(.Status=="missing")][0].Code')
S2N=$(echo "$SD2" | jq -r '[.Files[] | select(.Status=="missing")][0].FileName')
if [ -z "$S2C" ] || [ "$S2C" = "null" ]; then
  bad "无第二个缺失槽位"
else
  T2=$(now)
  upload_to_slot "$S2C" "$S2N" false && ok "init+upload（AutoExtract=false）成功" || bad "② init/upload 失败"
  if [ -n "${FCN:-}" ]; then
    FC2="$FCN"
    CONF=$(apost "/upload/confirm" "{\"TaskId\":\"$TID\",\"EnterpriseCode\":\"$ENT\",\"AutoExtract\":false}")
    echo "$CONF" | jq -e '.success==true' >/dev/null && ok "confirm（AutoExtract=false）受理" || bad "② confirm 失败: $(echo "$CONF" | jq -r '.err')"
    wait_idle "$CFG" 240 || bad "② 转换队列超时"
    wait_convert_done "$FC2" 90 || bad "② ConvertStatus=$(conv_of "$FC2")"
    sleep 8
    [ "$(extract_done_after "$FC2" "$T2")" -eq 0 ] && ok "② 未产生任何 extract_done（未入提取队列）" || bad "② 却产生了 $(extract_done_after "$FC2" "$T2") 条 extract_done"
    [ "$(st_of "$FC2")" = "none" ] && ok "② ExtractStatus = none" || bad "② ExtractStatus = '$(st_of "$FC2")'"
    case "$(msg_of "$FC2")" in *"未要求自动提取"*) ok "② ExtractMessage 记录原因" ;; *) bad "② ExtractMessage = '$(msg_of "$FC2")'" ;; esac
    assert3state "$FC2" "② 上传后"
  fi
fi

# ============================================================
sec "4. ③a 替换（AutoExtract=false）⇒ 读取口径为空（F）"
wait_idle "$CFG" 120 || bad "③a 替换前队列未空闲"
T3=$(now)
R=$(replace_file "$F" false "S2验收-替换-不自动提取")
echo "$R" | jq -e '.success==true' >/dev/null && ok "③a 替换受理" || bad "③a 替换失败: $(echo "$R" | jq -r '.err')"
[ "$(active_rows "$F")" -eq 0 ] && [ "$(result_fields "$F")" -eq 0 ] \
  && ok "③a 替换后活跃结果 = 0（口径为空）" || bad "③a 仍有 active=$(active_rows "$F") fields=$(result_fields "$F")"
assert3state "$F" "③a 替换后"
wait_idle "$CFG" 240 || bad "③a 转换队列超时"
wait_convert_done "$F" 90 || bad "③a ConvertStatus=$(conv_of "$F")"
sleep 6
[ "$(extract_done_after "$F" "$T3")" -eq 0 ] && ok "③a 未自动入提取队列（AutoExtract=false）" || bad "③a 却入了提取队列"
C=$(all_rows "$F"); [ "$C" -ge "$F_BASE" ] && ok "⑥ F 行数不减 $C >= $F_BASE" || bad "⑥ F 行数减少 $C < $F_BASE"

# ============================================================
sec "5. ③b 替换（AutoExtract=true）⇒ 换文件后自动重新提取（F）"
align_rule "$F_KEY"
T4=$(now)
R=$(replace_file "$F" true "S2验收-替换-自动提取")
echo "$R" | jq -e '.success==true' >/dev/null && ok "③b 替换受理" || bad "③b 替换失败: $(echo "$R" | jq -r '.err')"
wait_extract_done "$F" "$T4" 300 || bad "③b 替换后未自动重新提取（超时）"
[ "$(st_of "$F")" = "completed" ] && ok "③b 重新提取终态 completed" || bad "③b 终态=$(st_of "$F") msg=$(msg_of "$F")"
[ "$(active_rows "$F")" -ge 1 ] && ok "③b 读取口径已回填（活跃行=$(active_rows "$F")，字段=$(result_fields "$F")）" || bad "③b 活跃行=0"
C=$(all_rows "$F"); [ "$C" -ge "$F_BASE" ] && ok "⑥ F 行数不减 $C >= $F_BASE" || bad "⑥ F 行数减少 $C < $F_BASE"
assert3state "$F" "③b 重新提取后"

# ============================================================
sec "6. ④ 移除 FC1（v1 有结果）⇒ 读取口径为空"
if [ -n "${FC1:-}" ]; then
  wait_idle "$CFG" 120 || bad "④ 移除前队列未空闲"
  [ "$(active_rows "$FC1")" -ge 1 ] && ok "④ 删除前口径非空（active=$(active_rows "$FC1")）" || bad "④ 删除前口径已空，④ 断言无意义"
  D=$(apost "/delete" "{\"FileCode\":\"$FC1\",\"EnterpriseCode\":\"$ENT\",\"Reason\":\"S2验收-移除\"}")
  echo "$D" | jq -e '.success==true' >/dev/null && ok "④ 移除受理" || bad "④ 移除失败: $(echo "$D" | jq -r '.err // .message')"
  [ "$(active_rows "$FC1")" -eq 0 ] && [ "$(result_fields "$FC1")" -eq 0 ] \
    && ok "④ 移除后活跃结果 = 0（口径为空）" || bad "④ 仍有 active=$(active_rows "$FC1")"
  C=$(all_rows "$FC1"); [ "$C" -ge "${FC1_BASE:-0}" ] && ok "⑥ FC1 行数不减（只归档）$C >= ${FC1_BASE:-0}" || bad "⑥ FC1 行数减少 $C < ${FC1_BASE:-0}"
else
  bad "④ 无 FC1 可移除"
fi

# ============================================================
sec "7. ⑤ 恢复 FC1 到 v1 ⇒ v1 字段原样回来"
if [ -n "${FC1:-}" ]; then
  R=$(apost "/restore" "{\"FileCode\":\"$FC1\",\"EnterpriseCode\":\"$ENT\",\"VersionNumber\":1,\"Reason\":\"S2验收-恢复v1\"}")
  echo "$R" | jq -e '.success==true' >/dev/null && ok "⑤ 恢复 v1 受理" || bad "⑤ 恢复失败: $(echo "$R" | jq -r '.err // .message')"
  [ "$(active_rows "$FC1")" -ge 1 ] && [ "$(result_fields "$FC1")" -ge 1 ] \
    && ok "⑤ v1 结果已回活（活跃行=$(active_rows "$FC1")，字段=$(result_fields "$FC1")）" \
    || bad "⑤ 恢复后口径为空 active=$(active_rows "$FC1") fields=$(result_fields "$FC1")"
  C=$(all_rows "$FC1"); [ "$C" -ge "${FC1_BASE:-0}" ] && ok "⑥ FC1 行数不减 $C >= ${FC1_BASE:-0}" || bad "⑥ FC1 行数减少"
  assert3state "$FC1" "⑤ 恢复后"
else
  bad "⑤ 无 FC1 可恢复"
fi

# ============================================================
echo ""
echo "=================================================="
echo " S2 验收：通过 $PASS / 失败 $FAIL"
if [ "$FAIL" -gt 0 ]; then printf "%b\n" "$FAIL_DETAILS"; fi
echo "=================================================="
[ "$FAIL" -eq 0 ]
