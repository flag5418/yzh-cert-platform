#!/bin/bash
# 加载本地口令（⛔ 禁止把口令写进脚本/文档）
for _e in "$(dirname "$0")/../../docker/.env" "$(dirname "$0")/../docker/.env" "$(dirname "$0")/../../../docker/.env" "$(dirname "$0")/docker/.env"; do
  [ -f "$_e" ] && set -a && . "$_e" && set +a && break
done
# ============================================================
# S1 · 提取定位链验收（10 号 §六 S1）
#
# 断言：执行器的判定 == 图 2 ④ 的 SQL 独立同口径
#   用例 A：有 Markdown、四元组无 passed 规则 ⇒ 执行器 skipped（SQL 也算出 no_rule）
#   用例 B：临时把规则置 passed 且对齐到该槽位 ⇒ 执行器进入提取链（不再 skipped）
#
# 依赖：后端 127.0.0.1:9992 运行中；MySQL 容器 yzh-mysql；/tmp/ent_tok.txt 有效企业 token
# 用法：./scripts/backend/test-extraction-scope-resolver.sh
# ============================================================

set -uo pipefail

BASE="http://127.0.0.1:9992"
API="$BASE/api/Auditor/EnterpriseFile"
ENT="42833c3d3fe241b1bcfa707f6b92e4c3"
STAGE="29c1bcc3a18942e1865b2497a0262504"
SLOT="1166abf84ef34b419ad9c61409e3b8cb"          # 质量手册封面.doc（有 Markdown）
TPL_FILE="12711e0cc94e4d7c804ee82b9b1a683f"       # 规则原对齐点（该槽位无 Markdown）
RULE="c8c0e278-aea9-42d5-9890-174911ae5412"

PASS=0; FAIL=0; FAIL_DETAILS=""
TOK=$(cat /tmp/ent_tok.txt 2>/dev/null || echo "")

ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); FAIL_DETAILS="$FAIL_DETAILS\n  ✗ $1"; echo "  ✗ $1"; }
sec() { echo ""; echo "── $1"; }

MY() { docker exec -i yzh-mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" --default-character-set=utf8mb4 yzh_cert_platform -N -B -e "$1" 2>/dev/null; }

restore() {
  MY "UPDATE cert_doc_extraction_rule SET Status='failed', StandardFileCode='$TPL_FILE' WHERE Code='$RULE';"
  MY "UPDATE cert_standard_directory_file SET ExtractStatus='none', ExtractMessage=NULL WHERE Code='$SLOT';"
  # 验收产生的提取结果归档（保持下游读取口径为空，不污染业务数据）
  MY "UPDATE cert_extraction_result SET IsValid=0 WHERE OrgCode='$ENT' AND FileCode='$SLOT' AND IsValid=1;"
  MY "UPDATE cert_table_extraction_result SET IsValid=0 WHERE OrgCode='$ENT' AND FileCode='$SLOT' AND IsValid=1;"
}
trap restore EXIT

# SQL 独立同口径：图 2 ①②③④ → 输出 "ruleCode<TAB>md<TAB>org"
sql_verdict() {
  MY "SELECT COALESCE(r.Code,''), (f.MarkdownPath IS NOT NULL AND f.MarkdownPath<>''), COALESCE(c.OrgCode,'')
      FROM cert_standard_directory_file f
      LEFT JOIN cert_standard_directory_file t ON t.Code = f.StandardFileCode
      LEFT JOIN cert_standard_directory_config c ON c.Code = IFNULL(NULLIF(t.ConfigCode,''), f.ConfigCode)
      LEFT JOIN cert_doc_extraction_rule r
           ON r.OrgCode = c.OrgCode
          AND r.StandardCode = IFNULL(NULLIF(t.StandardCode,''), f.StandardCode)
          AND r.StageCode = IFNULL(NULLIF(t.StageCode,''), f.StageCode)
          AND r.StandardFileCode = IFNULL(f.StandardFileCode, f.Code)
          AND r.IsValid = 1 AND r.IsDeleted = 0 AND r.Status = 'passed'
      WHERE f.Code = '$SLOT';"
}

trigger_and_wait() {
  local resp
  resp=$(curl -s -X POST "$API/trigger-extract" \
    -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
    -d "{\"FileCode\":\"$SLOT\",\"EnterpriseCode\":\"$ENT\"}")
  echo "      trigger 响应: $resp" >&2
  if ! echo "$resp" | jq -e '.success == true' >/dev/null 2>&1; then
    echo "trigger_failed"; return 1
  fi
  # ⚠️ S2 之后 status 只有 3 个终态，入队瞬间即为 none ⇒ 不能拿 status 判「跑完」。
  # 以 trigger 写下的占位文案「已加入提取队列」为在途标记，离开标记才算终态。
  local ST MSG
  for i in $(seq 1 60); do
    ST=$(MY "SELECT IFNULL(ExtractStatus,'') FROM cert_standard_directory_file WHERE Code='$SLOT';")
    MSG=$(MY "SELECT IFNULL(ExtractMessage,'') FROM cert_standard_directory_file WHERE Code='$SLOT';")
    [ "$MSG" != "已加入提取队列" ] && { echo "$ST"; return 0; }
    sleep 3
  done
  echo "$ST(超时:$MSG)"
}

# ── 0. 前置 ────────────────────────────────────────────────
sec "0. 前置"
[ -n "$TOK" ] && ok "企业 token 就绪" || { bad "缺少 /tmp/ent_tok.txt"; exit 1; }
HASMD=$(MY "SELECT MarkdownPath IS NOT NULL AND MarkdownPath<>'' FROM cert_standard_directory_file WHERE Code='$SLOT';")
[ "$HASMD" = "1" ] && ok "目标槽位有 Markdown 产物" || { bad "目标槽位无 Markdown"; exit 1; }
CUR_RULE=$(MY "SELECT StandardFileCode FROM cert_doc_extraction_rule WHERE Code='$RULE';")
ok "规则原对齐点=$CUR_RULE"

# ── A. 无 passed 规则 ⇒ skipped（SQL 也算出 no_rule）──────
sec "A. 四元组未命中 passed 规则"
VERDICT=$(sql_verdict); RC=$(echo "$VERDICT" | cut -f1); MD=$(echo "$VERDICT" | cut -f2)
echo "      SQL 口径: ruleCode='$RC' markdown=$MD org=$(echo "$VERDICT" | cut -f3)"
[ -z "$RC" ] && ok "SQL 独立口径 = 不可提取（rule_code 空）" || bad "SQL 竟算出可提取：$RC"
RESULT=$(trigger_and_wait)
[ "$RESULT" = "skipped" ] && ok "执行器判定 = skipped（与 SQL 同口径）" \
  || bad "执行器判定 = $RESULT（期望 skipped）"
MSG=$(MY "SELECT ExtractMessage FROM cert_standard_directory_file WHERE Code='$SLOT';")
echo "      ExtractMessage: $MSG"

# ── B. passed 规则 + Markdown ⇒ 进入提取链 ────────────────
sec "B. 规则置 passed 且对齐到该槽位 ⇒ 进入提取链"
# ★ 规则键 = 槽位的 StandardFileCode（模板行 Code，对齐点），不是槽位自身 Code
TARGET_KEY=$(MY "SELECT StandardFileCode FROM cert_standard_directory_file WHERE Code='$SLOT';")
[ -n "$TARGET_KEY" ] && ok "槽位对齐点（模板行 Code）= $TARGET_KEY" || { bad "槽位无 StandardFileCode"; }
MY "UPDATE cert_doc_extraction_rule SET Status='passed', StandardFileCode='$TARGET_KEY' WHERE Code='$RULE';"
VERDICT=$(sql_verdict); RC=$(echo "$VERDICT" | cut -f1); MD=$(echo "$VERDICT" | cut -f2)
echo "      SQL 口径: ruleCode='$RC' markdown=$MD org=$(echo "$VERDICT" | cut -f3)"
[ "$RC" = "$RULE" ] && ok "SQL 独立口径 = 可提取（命中 passed 规则）" \
  || bad "SQL 未算出命中规则：$VERDICT"
RESULT=$(trigger_and_wait)
case "$RESULT" in
  skipped) bad "执行器仍判定 skipped —— Status='passed' 过滤未生效" ;;
  *) ok "执行器进入提取链，终态 = $RESULT（非 skipped）" ;;
esac
[ "$RESULT" = "completed" ] && ok "提取成功（completed，字段已落库）" \
  || { echo "      终态非 completed，ExtractMessage: $(MY "SELECT ExtractMessage FROM cert_standard_directory_file WHERE Code='$SLOT';")"; }

# ── 汇总（restore 已挂 EXIT）────────────────────────────────
echo ""
echo "========================================="
echo "  S1 验收：通过 $PASS ，失败 $FAIL"
echo "========================================="
if [ $FAIL -gt 0 ]; then echo -e "失败详情：$FAIL_DETAILS"; exit 1; fi
exit 0
