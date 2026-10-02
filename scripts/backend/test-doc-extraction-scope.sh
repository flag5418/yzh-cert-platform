#!/bin/bash
# ============================================================
# S0 · 规则四元组（机构维度）验收
# 对应：docs/40-实施/企业资料管理/10-执行计划-V1.md §六 S0
#
# 断言：
#   1. 真实模板文件的规则详情能取到 OrgCode（回填正确）
#   2. 同一 StandardFileCode、不同 OrgCode 的两条规则可共存（uk_rule_scope 生效）
#   3. 详情按四元组命中「本机构」那条，不会串到另一机构
#   4. 规则 IsValid=0 ⇒ 详情查不到（全企业视为「无规则」）
#   5. 不存在的文件行 + 未传 orgCode ⇒ 保存被拒（避免造出推不出作用域的孤儿规则）
#
# 依赖：后端 127.0.0.1:9992 运行中；MySQL 容器 yzh-mysql；jq；curl
# 用法：./scripts/backend/test-doc-extraction-scope.sh
# ============================================================

set -uo pipefail

BASE="http://127.0.0.1:9992"
API="$BASE/api/Workflow/DocExtractionRule"
USER="admin"
PWD_="123456"

# 固定测试锚点（实测数据，见 10 号 §九）
TPL_FILE="12711e0cc94e4d7c804ee82b9b1a683f"   # 模板行：附录三 程序文件清单.doc（config 3881ecae…）
TPL_ORG="906e8b2a962c4062b21144af4cc4abc0"     # 雄安尚龙（模板所属机构）
TPL_STD="846dec4b-c534-4983-94e6-8cf04982b7d9" # ISO 9001
TPL_STG="29c1bcc3a18942e1865b2497a0262504"     # 阶段复审
SIBLING_ORG="TEST-ORG-SCOPE"                    # 冒充的「另一机构」

PASS=0; FAIL=0; FAIL_DETAILS=""
TOKEN=""
TMPDIR_T="$(mktemp -d)"
trap 'cleanup' EXIT

MY() { docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 yzh_cert_platform -N -B -e "$1" 2>/dev/null; }

ok()  { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad() { FAIL=$((FAIL+1)); FAIL_DETAILS="$FAIL_DETAILS\n  ✗ $1"; echo "  ✗ $1"; }
sec() { echo ""; echo "── $1"; }

cleanup() {
  # 无论成败，清掉冒充机构的兄弟规则，避免污染
  MY "DELETE FROM cert_doc_extraction_rule WHERE OrgCode='$SIBLING_ORG';"
  MY "UPDATE cert_doc_extraction_rule SET IsValid=1 WHERE StandardFileCode='$TPL_FILE';"
  rm -rf "$TMPDIR_T"
}

req() {
  local method="$1" url="$2" body="${3:-}"
  local args=(-s -o "$TMPDIR_T/resp.json" -w '%{http_code}' -X "$method" "$url"
              -H 'Content-Type: application/json'
              -H "Authorization: Bearer $TOKEN")
  [ -n "$body" ] && args+=(-d "$body")
  curl "${args[@]}"
}

assert() {
  local expr="$1" desc="$2"
  if jq -e "$expr" "$TMPDIR_T/resp.json" >/dev/null 2>&1; then ok "$desc"; else
    bad "$desc"; echo "      resp: $(head -c 400 "$TMPDIR_T/resp.json")"
  fi
}

# ── DDL 前置检查 ────────────────────────────────────────────
sec "0. 登录 + DDL 前置"
code=$(curl -s -o "$TMPDIR_T/resp.json" -w '%{http_code}' -X POST "$BASE/api/User/login" \
  -H 'Content-Type: application/json' -d "{\"UserName\":\"$USER\",\"Password\":\"$PWD_\"}")
TOKEN=$(jq -r '.data.Token // .data.token // empty' "$TMPDIR_T/resp.json" 2>/dev/null)
[ -n "$TOKEN" ] && ok "登录成功" || { bad "登录失败"; exit 1; }

COLS=$(MY "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_doc_extraction_rule' AND COLUMN_NAME='OrgCode';")
[ "$COLS" = "1" ] && ok "cert_doc_extraction_rule.OrgCode 已存在" || bad "OrgCode 列缺失（DDL 未执行）"

UK=$(MY "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_doc_extraction_rule' AND INDEX_NAME='uk_rule_scope';")
[ "$UK" = "4" ] && ok "uk_rule_scope 四列唯一键已建立" || bad "uk_rule_scope 缺失或列数!=4（实际 $UK）"

OLDUK=$(MY "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_doc_extraction_rule' AND INDEX_NAME='uk_standard_file_code';")
[ "$OLDUK" = "0" ] && ok "旧单列唯一键 uk_standard_file_code 已移除" || bad "uk_standard_file_code 仍在"

NULLORG=$(MY "SELECT COUNT(*) FROM cert_doc_extraction_rule WHERE OrgCode IS NULL OR OrgCode='';")
[ "$NULLORG" = "0" ] && ok "存量规则 OrgCode 全部回填（0 行为空）" || bad "仍有 $NULLORG 行 OrgCode 为空"

# ── 1. 真实模板文件详情（四元组命中 + orgCode 回显）────────────
sec "1. GET {tplFile} - 真实模板文件规则详情"
code=$(req GET "$API/$TPL_FILE")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert ".data.orgCode == \"$TPL_ORG\"" "orgCode = 模板所属机构（四元组回填正确）"
assert ".data.standardCode == \"$TPL_STD\"" "standardCode 正确"
assert ".data.stageCode == \"$TPL_STG\"" "stageCode 正确"
REAL_RULE_CODE=$(jq -r '.data.code // ""' "$TMPDIR_T/resp.json")
[ -n "$REAL_RULE_CODE" ] && ok "取到真实规则 Code=$REAL_RULE_CODE" || bad "未取到规则 Code"

# ── 2. 共存：同 StandardFileCode、不同 OrgCode ────────────────
sec "2. 插入兄弟规则（同文档 Code、不同机构）⇒ 两行共存"
SIB_CODE=$(MY "SELECT UUID();")
MY "INSERT INTO cert_doc_extraction_rule
      (Code, OrgCode, StandardFileCode, StandardCode, StageCode, Skill, Prompt, Status, DocIsValid, IsValid, IsDeleted, CreateTime)
    VALUES ('$SIB_CODE','$SIBLING_ORG','$TPL_FILE','$TPL_STD','$TPL_STG','word','','none',0,1,0,NOW());"
CNT=$(MY "SELECT COUNT(*) FROM cert_doc_extraction_rule WHERE StandardFileCode='$TPL_FILE';")
[ "$CNT" = "2" ] && ok "同一 StandardFileCode 下 2 条规则共存（uk_rule_scope 允许跨机构）" || bad "实际行数=$CNT（期望 2，说明唯一键仍是单列或插入失败）"

# ── 3. 四元组命中正确那条 ────────────────────────────────────
sec "3. GET {tplFile} - 四元组应命中本机构规则，不串机构"
code=$(req GET "$API/$TPL_FILE")
assert '.code == 200' "code=200"
assert ".data.code == \"$REAL_RULE_CODE\"" "命中的是本机构规则 Code，而非兄弟规则"
assert ".data.orgCode == \"$TPL_ORG\"" "orgCode 仍是模板机构"

# ── 4. 规则 IsValid=0 ⇒ 视为无规则 ──────────────────────────
sec "4. 规则 IsValid=0 ⇒ 详情 404（全企业视为无规则）"
MY "UPDATE cert_doc_extraction_rule SET IsValid=0 WHERE StandardFileCode='$TPL_FILE' AND OrgCode='$TPL_ORG';"
code=$(req GET "$API/$TPL_FILE")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 404' "code=404（disabled 规则不可见）"
MY "UPDATE cert_doc_extraction_rule SET IsValid=1 WHERE StandardFileCode='$TPL_FILE' AND OrgCode='$TPL_ORG';"

# ── 5. 孤儿规则防呆：推不出机构时保存被拒 ───────────────────
sec "5. POST save - 推不出 OrgCode 且未显式传 ⇒ 拒绝保存"
code=$(req POST "$API/save" '{"fileCode":"FL-scope-unknown","skill":"word","fields":[],"tables":[],"prompt":"x","isValid":true}')
[ "$code" = "200" ] && ok "HTTP 200（业务拒绝走信封）" || bad "HTTP $code"
assert '.success == false' "success=false"
ORPHAN=$(MY "SELECT COUNT(*) FROM cert_doc_extraction_rule WHERE StandardFileCode='FL-scope-unknown';")
[ "$ORPHAN" = "0" ] && ok "未产生孤儿规则" || bad "产生孤儿规则 $ORPHAN 行"

# ── 汇总 ────────────────────────────────────────────────────
echo ""
echo "========================================="
echo "  S0 验收：通过 $PASS ，失败 $FAIL"
echo "========================================="
if [ $FAIL -gt 0 ]; then
  echo -e "失败详情：$FAIL_DETAILS"
  exit 1
fi
exit 0
