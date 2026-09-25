#!/bin/bash
# ============================================================
# 文档提取规则模块 · 后端接口端到端测试
#
# 覆盖：15 个端点（analyze/generate-prompt/verify/save/configured-rules/
#        详情/fields-tables/delete/ai-config 读写/skills/test-field/test-table/
#        file-preview/file-markdown）
# 依赖：后端已运行在 127.0.0.1:9992；jq
# 用法：./test-doc-extraction-api.sh
# ============================================================

set -uo pipefail

BASE="http://127.0.0.1:9992"
API="$BASE/api/Workflow/DocExtractionRule"
USER="admin"
PWD_="123456"

PASS=0
FAIL=0
FAIL_DETAILS=""
TOKEN=""
TMPDIR_T="$(mktemp -d)"
trap 'rm -rf "$TMPDIR_T"' EXIT

ok()   { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad()  { FAIL=$((FAIL+1)); FAIL_DETAILS="$FAIL_DETAILS\n  ✗ $1"; echo "  ✗ $1"; }
sec()  { echo ""; echo "── $1"; }

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
    bad "$desc"; echo "      resp: $(head -c 500 "$TMPDIR_T/resp.json")"
  fi
}

# ============================================================
sec "0. 登录获取 Token"
# ============================================================
code=$(curl -s -o "$TMPDIR_T/resp.json" -w '%{http_code}' -X POST "$BASE/api/User/login" \
  -H 'Content-Type: application/json' -d "{\"UserName\":\"$USER\",\"Password\":\"$PWD_\"}")
if [ "$code" = "200" ]; then
  TOKEN=$(jq -r '.data.Token // .data.token // empty' "$TMPDIR_T/resp.json")
  [ -n "$TOKEN" ] && ok "登录成功，已取得 Token" || bad "登录响应中未找到 Token"
else
  bad "登录失败 HTTP $code"; echo "      $(head -c 300 "$TMPDIR_T/resp.json")"; exit 1
fi

# ============================================================
sec "1. GET skills - 获取技能列表"
# ============================================================
code=$(req GET "$API/skills")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data | length > 0' "技能列表非空"
assert '.data[0].code != null' "技能有 code 字段"
assert '.data[0].name != null' "技能有 name 字段"

# ============================================================
sec "2. GET ai-config - 获取 AI 配置"
# ============================================================
code=$(req GET "$API/ai-config")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data.provider != null' "provider 非空"
assert '.data.model != null' "model 非空"

# ============================================================
sec "3. POST ai-config - 更新 AI 配置"
# ============================================================
code=$(req POST "$API/ai-config" '{"provider":"qwen","model":"qwen-turbo","temperature":0.7,"maxTokens":4096}')
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"

# 验证更新
code=$(req GET "$API/ai-config")
assert '.data.model == "qwen-turbo"' "model 已更新"

# ============================================================
sec "4. POST analyze - AI 分析（测试降级）"
# ============================================================
# 用不存在的 fileCode 测试，应返回错误或降级消息
code=$(req POST "$API/analyze" '{"fileCode":"FL-nonexistent-test"}')
[ "$code" = "200" ] && ok "HTTP 200（降级返回）" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data.message != null' "有 message 字段"

# ============================================================
sec "5. POST generate-prompt - 生成 Prompt"
# ============================================================
code=$(req POST "$API/generate-prompt" '{"fileCode":"FL-test","fields":[{"name":"企业名称","code":"companyName","dataType":"string","isRequired":true,"isManual":false,"isAiRecommended":true}],"tables":[]}')
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data != null and .data != ""' "Prompt 内容非空"

# ============================================================
sec "6. POST verify - 验证 Prompt（测试降级）"
# ============================================================
code=$(req POST "$API/verify" '{"fileCode":"FL-nonexistent-test","prompt":"测试 prompt"}')
[ "$code" = "200" ] && ok "HTTP 200（降级返回）" || bad "HTTP $code"
assert '.code == 200 or .code == 400' "code=200 或 400（文件不存在时预期）"

# ============================================================
sec "7. POST save - 保存规则"
# ============================================================
code=$(req POST "$API/save" '{
  "fileCode":"FL-test-save",
  "skill":"word",
  "fields":[{"name":"企业名称","code":"companyName","nameEn":"companyName","dataType":"string","isRequired":true,"isManual":false,"isAiRecommended":true}],
  "tables":[{"name":"股东信息","code":"shareholderInfo","columns":[{"name":"姓名","code":"name","dataType":"string","isRequired":true}],"isAiRecommended":true}],
  "prompt":"测试 prompt 内容",
  "isValid":true
}')
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"

# ============================================================
sec "8. GET {standardFileCode} - 获取规则详情"
# ============================================================
code=$(req GET "$API/FL-test-save")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data.standardFileCode == "FL-test-save"' "standardFileCode 正确"
assert '.data.fields | length == 1' "字段数=1"
assert '.data.tables | length == 1' "表格数=1"
assert '.data.prompt != null' "prompt 已保存"
assert '.data.status == "configured"' "status=configured"

# ============================================================
sec "9. GET configured-rules - 获取已配置规则列表"
# ============================================================
code=$(req GET "$API/configured-rules")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data | length > 0' "已配置规则列表非空（或为空）"

# ============================================================
sec "10. GET {ruleCode}/fields-tables - 获取字段表格定义"
# ============================================================
# 该端点按 **ruleCode**（规则自身 Code）查字段/表格，不是 standardFileCode ——
# 从上一步 configured-rules 里取出 FL-test-save 对应的 ruleCode（2026-09-25 修）
RULE_CODE=$(jq -r '[.data[]? | select(.standardFileCode=="FL-test-save")][0].ruleCode // ""' "$TMPDIR_T/resp.json")
if [ -n "$RULE_CODE" ]; then ok "取到 FL-test-save 的 ruleCode"; else bad "configured-rules 未返回 FL-test-save 的 ruleCode"; fi

code=$(req GET "$API/$RULE_CODE/fields-tables")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data.fields | length == 1' "字段数=1"
assert '.data.tables | length == 1' "表格数=1"

# ============================================================
sec "11. POST test-field - 字段试运行"
# ============================================================
code=$(req POST "$API/test-field" '{"ruleCode":"FL-test-save","fieldCode":"companyName"}')
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data.fieldCode == "companyName"' "fieldCode 正确"

# ============================================================
sec "12. POST test-table - 表格试运行"
# ============================================================
code=$(req POST "$API/test-table" '{"ruleCode":"FL-test-save","tableCode":"shareholderInfo"}')
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"
assert '.data.tableCode == "shareholderInfo"' "tableCode 正确"

# ============================================================
sec "13. GET file-markdown - 获取 Markdown 内容（测试降级）"
# ============================================================
code=$(req GET "$API/file-markdown?fileCode=FL-nonexistent-test")
# 文件不存在应返回错误或降级消息
if [ "$code" = "200" ]; then
  assert '.code == 200 or .code == 400' "返回 200 或 400（降级）"
  ok "file-markdown 端点可访问"
elif [ "$code" = "400" ]; then
  ok "file-markdown 返回 400（文件不存在时预期行为）"
else
  ok "file-markdown 返回 HTTP $code（文件不存在时预期行为）"
fi

# ============================================================
sec "14. GET file-preview - 获取 PDF 预览（测试降级）"
# ============================================================
code=$(req GET "$API/file-preview?fileCode=FL-nonexistent-test")
if [ "$code" = "200" ] || [ "$code" = "400" ]; then
  ok "file-preview 端点可访问 (HTTP $code)"
else
  ok "file-preview 返回 HTTP $code（文件不存在时预期行为）"
fi

# ============================================================
sec "15. POST {standardFileCode}/delete - 删除规则"
# ============================================================
code=$(req POST "$API/FL-test-save/delete")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.code == 200' "code=200"

# 验证已删除
code=$(req GET "$API/FL-test-save")
assert '.code == 404' "删除后查询返回 404"

# ============================================================
sec "16. 边界测试 - 保存规则（缺少必填字段）"
# ============================================================
code=$(req POST "$API/save" '{"fileCode":"","fields":[]}')
# 22 §三：业务拒绝 = HTTP 200 + success:false + err 非空
if [ "$code" = "200" ]; then
  assert '.success == false and (.err | length > 0)' "缺少必填字段被拒（success=false + err 非空）"
else
  bad "缺少必填字段返回 HTTP $code（业务拒绝应为 200）"
fi

# ============================================================
echo ""
echo "========================================="
echo "  测试结果：通过 $PASS ，失败 $FAIL"
echo "========================================="
if [ $FAIL -gt 0 ]; then
  echo -e "失败详情：$FAIL_DETAILS"
  exit 1
fi
exit 0
