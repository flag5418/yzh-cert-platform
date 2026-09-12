#!/bin/bash
# ============================================================
# 数据字典模块 · 后端接口端到端测试
#
# 覆盖：配置端点 / 树端点 / 右表端点 / 字典消费端点 / 软删除语义
# 依赖：后端已运行在 127.0.0.1:9992；jq
# 用法：./test-dictionary-api.sh
#
# 说明：写操作会创建临时数据（名称前缀 __T__），测试结束自动物理清理，
#       不留残留（软删除的测试行由清理步骤直接 DELETE 掉）。
# ============================================================

set -uo pipefail

BASE="http://127.0.0.1:9992"
API="$BASE/api/System/Dictionary"
CTRL="$BASE/api/Dictionary"
USER="admin"
PWD_="123456"

PASS=0
FAIL=0
TOKEN=""
TMPDIR_T="$(mktemp -d)"
trap 'rm -rf "$TMPDIR_T"' EXIT

ok()   { PASS=$((PASS+1)); echo "  ✓ $1"; }
bad()  { FAIL=$((FAIL+1)); echo "  ✗ $1"; }
sec()  { echo ""; echo "── $1"; }

# req <method> <url> [body] -> 输出 body 到 $TMPDIR_T/resp.json，返回 http code
req() {
  local method="$1" url="$2" body="${3:-}"
  local args=(-s -o "$TMPDIR_T/resp.json" -w '%{http_code}' -X "$method" "$url"
              -H 'Content-Type: application/json'
              -H "Authorization: Bearer $TOKEN")
  [ -n "$body" ] && args+=(-d "$body")
  curl "${args[@]}"
}

# 断言：jq 表达式结果为 true
assert() {
  local expr="$1" desc="$2"
  if jq -e "$expr" "$TMPDIR_T/resp.json" >/dev/null 2>&1; then ok "$desc"; else
    bad "$desc"; echo "      resp: $(head -c 400 "$TMPDIR_T/resp.json")"
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
sec "1. 配置端点 /treepconfig"
# ============================================================
code=$(req GET "$API/treepconfig")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.success == true'                          "success=true"
assert '.data.TableConfig.Columns | length > 0'    "TableConfig.Columns 非空（右表 EntityConfig 已加载）"
assert '.data.TreeConfig.RelateField == "DicCode"' "TreeConfig.RelateField = DicCode"
assert '.data.TreeConfig.NameField == "DicName"'   "TreeConfig.NameField = DicName"
assert '.data.TreeConfig.EnableField == "IsValid"' "TreeConfig.EnableField = IsValid"
assert '.data.TreeConfig.AllowDeleteWithChildren == true' "TreeConfig.AllowDeleteWithChildren = true（级联软删除）"
assert '.data.TreeFormConfig.Columns | length > 0' "TreeFormConfig.Columns 非空（树表单已加载）"

# ============================================================
sec "2. 树端点 /tree/root 与 /tree/children"
# ============================================================
code=$(req POST "$API/tree/root" '{}')
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.data | length == 4' "根节点 4 个"

ROOT_CODE=$(jq -r '.data[] | select(.Name=="认证平台字典") | .Code' "$TMPDIR_T/resp.json" | head -1)
[ -n "$ROOT_CODE" ] && ok "取得「认证平台字典」根节点 Code" || bad "未找到「认证平台字典」根节点"

code=$(req POST "$API/tree/children" "{\"ParentCode\":\"$ROOT_CODE\",\"Level\":0}")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.data | length == 17' "认证平台字典下 17 个子字典"
assert '.data[0].ParentCode == "'"$ROOT_CODE"'"' "子节点 ParentCode 正确回填"
assert '.data[0].IsLeaf == true' "子字典 IsLeaf=true（批量计算生效）"
assert '.data[0].Extra.IsValid == 1' "树节点 Extra 携带 IsValid（供前端切换按钮文案）"

DICT_CODE=$(jq -r '.data[] | select(.Name=="证书状态") | .Code' "$TMPDIR_T/resp.json" | head -1)
[ -z "$DICT_CODE" ] && DICT_CODE=$(jq -r '.data[0].Code' "$TMPDIR_T/resp.json")
[ -n "$DICT_CODE" ] && ok "取得子字典 Code 用于右表测试" || bad "未取得子字典 Code"

# ============================================================
sec "3. 右表端点 /filter（树过滤注入）"
# ============================================================
code=$(req POST "$API/filter" "{\"Page\":1,\"PageSize\":20,\"Filters\":[{\"Field\":\"DicCode\",\"Operator\":\"eq\",\"Value\":\"$DICT_CODE\"}]}")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.success == true'                 "success=true"
assert '.data.Items | length > 0'         "返回该字典下的字典项"
assert '.data.Items[0].DicCode == "'"$DICT_CODE"'"' "字典项 DicCode 与树节点一致（树→表联动生效）"
assert '.data.Items[0].Code != null'      "字典项含 Code（框架已自动生成随机唯一值）"

# 未选中树节点 → NoSelectionBehavior=empty 应返回空页
code=$(req POST "$API/filter" '{"Page":1,"PageSize":20,"Filters":[]}')
assert '.data.TotalCount == 0' "未选中树节点时返回空页（NoSelectionBehavior=empty）"

# ============================================================
sec "4. 字典消费端点 /items/{code}"
# ============================================================
code=$(req GET "$API/items/$DICT_CODE")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.success == true'          "success=true"
assert '.data | length > 0'        "返回字典项选项列表"
assert '.data[0].Value != null and .data[0].Value != ""' "选项 Value 非空（= 字典项 Code）"
assert '.data[0].Label != null and .data[0].Label != ""' "选项 Label 非空（= 显示文本）"
assert '([.data[].Value] | length) == ([.data[].Value] | unique | length)' "Value 全局唯一"

# 禁用项必须被过滤：临时把该字典下第一项禁用，再取列表应少一项
FIRST_ITEM=$(jq -r '.data[0].Value' "$TMPDIR_T/resp.json")
BEFORE=$(jq -r '.data | length' "$TMPDIR_T/resp.json")
code=$(req POST "$API/toggle-valid" "{\"Code\":\"$FIRST_ITEM\"}")
assert '.data.IsValid == 0' "toggle-valid 将字典项置为禁用（IsValid=0）"
code=$(req GET "$API/items/$DICT_CODE")
AFTER=$(jq -r '.data | length' "$TMPDIR_T/resp.json")
[ "$AFTER" -eq "$((BEFORE-1))" ] && ok "已禁用项被自动过滤（$BEFORE → $AFTER）" || bad "禁用项未被过滤（$BEFORE → $AFTER）"
# 复原
req POST "$API/toggle-valid" "{\"Code\":\"$FIRST_ITEM\"}" >/dev/null
assert '.data.IsValid == 1' "toggle-valid 复原为启用（IsValid=1）"

# ============================================================
sec "5. 分类消费端点 /category/{code}/dictionaries"
# ============================================================
code=$(req GET "$API/category/$ROOT_CODE/dictionaries")
[ "$code" = "200" ] && ok "HTTP 200" || bad "HTTP $code"
assert '.success == true'   "success=true"
assert '.data | length == 17' "返回该分类下 17 个字典"
assert '.data[0].Value != null and .data[0].Label != null' "选项含 Value/Label"

# ============================================================
sec "6. 写路径：新增字典（不填字典编码 DicNo）"
# ============================================================
NEW_NAME="__T__字典$(date +%s)"
code=$(req POST "$API/tree/add" "{\"DicName\":\"$NEW_NAME\",\"ParentCode\":\"$ROOT_CODE\"}")
[ "$code" = "200" ] && ok "HTTP 200（DicNo 未填也能新增，证明编码非必填）" || bad "HTTP $code"
assert '.success == true' "success=true"
NEW_CODE=$(jq -r '.data.Code' "$TMPDIR_T/resp.json")
assert '.data.Code | length == 32' "框架自动生成 32 位随机 Code（无连字符 GUID）"
assert '.data.Name == "'"$NEW_NAME"'"' "返回节点名称正确"

# 同名重复应被拒
code=$(req POST "$API/tree/add" "{\"DicName\":\"$NEW_NAME\",\"ParentCode\":\"$ROOT_CODE\"}")
[ "$code" = "400" ] && ok "同级重名被拒（HTTP 400）" || bad "同级重名未被拒（HTTP $code）"

# 修改
NEW_NAME2="${NEW_NAME}改"
code=$(req POST "$API/tree/update" "{\"Code\":\"$NEW_CODE\",\"DicName\":\"$NEW_NAME2\",\"ParentCode\":\"$ROOT_CODE\",\"DicNo\":\"__t_dict_no\"}")
[ "$code" = "200" ] && ok "tree/update HTTP 200" || bad "tree/update HTTP $code"
assert '.data.Name == "'"$NEW_NAME2"'"' "名称已更新"
assert '.data.Code == "'"$NEW_CODE"'"'  "Code 保持不变（Code 不可修改）"

# 字典编码唯一性
code=$(req POST "$API/tree/add" "{\"DicName\":\"${NEW_NAME}另一\",\"ParentCode\":\"$ROOT_CODE\",\"DicNo\":\"__t_dict_no\"}")
[ "$code" = "400" ] && ok "字典编码重复被拒（HTTP 400）" || bad "字典编码重复未被拒（HTTP $code）"

# 树节点启用/禁用
code=$(req POST "$API/tree/toggle-valid" "{\"Code\":\"$NEW_CODE\"}")
assert '.data.IsValid == 0' "tree/toggle-valid 禁用成功"
code=$(req POST "$API/tree/toggle-valid" "{\"Code\":\"$NEW_CODE\"}")
assert '.data.IsValid == 1' "tree/toggle-valid 启用成功"

# ============================================================
sec "7. 写路径：字典项增删改"
# ============================================================
ITEM_NAME="__T__项$(date +%s)"
code=$(req POST "$API/add" "{\"DicCode\":\"$NEW_CODE\",\"DicName\":\"$ITEM_NAME\",\"DicValue\":\"v1\",\"OrderNo\":1}")
[ "$code" = "200" ] && ok "新增字典项 HTTP 200" || bad "新增字典项 HTTP $code"
ITEM_CODE=$(jq -r '.data.Code' "$TMPDIR_T/resp.json")
assert '.data.Code | length == 32' "字典项 Code 为 32 位随机值"
assert '.data.DicCode == "'"$NEW_CODE"'"' "DicCode 已写入"

# 同字典内重名应被拒
code=$(req POST "$API/add" "{\"DicCode\":\"$NEW_CODE\",\"DicName\":\"$ITEM_NAME\"}")
[ "$code" = "400" ] && ok "同字典内重名被拒（HTTP 400）" || bad "同字典内重名未被拒（HTTP $code）"

# 缺少所属字典应被拒
code=$(req POST "$API/add" "{\"DicCode\":\"\",\"DicName\":\"x\"}")
[ "$code" = "400" ] && ok "缺少所属字典被拒（HTTP 400）" || bad "缺少所属字典未被拒（HTTP $code）"

# 修改
code=$(req POST "$API/update" "{\"Code\":\"$ITEM_CODE\",\"DicCode\":\"$NEW_CODE\",\"DicName\":\"${ITEM_NAME}改\",\"DicValue\":\"v2\",\"OrderNo\":2,\"IsValid\":1}")
[ "$code" = "200" ] && ok "修改字典项 HTTP 200" || bad "修改字典项 HTTP $code"

# 消费端点应能取到刚新增的项
code=$(req GET "$API/items/$NEW_CODE")
assert '.data | length == 1' "新增的字典项可被 /items 消费"

# 删除字典项（软删除）
code=$(req POST "$API/delete" "[\"$ITEM_CODE\"]")
[ "$code" = "200" ] && ok "删除字典项 HTTP 200" || bad "删除字典项 HTTP $code"
code=$(req GET "$API/items/$NEW_CODE")
assert '.data | length == 0' "已删除字典项不再出现在消费列表中"

# ============================================================
sec "8. 删除语义：分类级联软删除"
# ============================================================
code=$(req POST "$API/tree/delete" "[\"$NEW_CODE\"]")
[ "$code" = "200" ] && ok "tree/delete HTTP 200（含子节点直接删除，无前置业务校验）" || bad "tree/delete HTTP $code"
code=$(req POST "$API/tree/children" "{\"ParentCode\":\"$ROOT_CODE\",\"Level\":0}")
assert '[.data[].Code] | index("'"$NEW_CODE"'") == null' "已删除字典不再出现在树中"

# 前端调用形式（/api/Dictionary/...）也要通
code=$(req GET "$CTRL/treepconfig")
[ "$code" = "200" ] && ok "兼容路由 /api/Dictionary/treepconfig 可用" || bad "兼容路由 HTTP $code"
code=$(req POST "$CTRL/tree/root" '{}')
[ "$code" = "200" ] && ok "兼容路由 /api/Dictionary/tree/root 可用" || bad "兼容路由 HTTP $code"

# ============================================================
sec "9. 数据库侧核对：软删除而非物理删除"
# ============================================================
MYSQL_PWD_='Yzh123456.'
row=$(docker exec -i -e MYSQL_PWD="$MYSQL_PWD_" yzh-mysql mysql -uroot -N -B \
  yzh_cert_platform -e "SELECT CONCAT(IsDeleted,'|',IFNULL(DeleteBy,'NULL'),'|',IFNULL(DeleteTime,'NULL')) FROM Sys_Dictionary WHERE Code='$NEW_CODE';" 2>/dev/null)
echo "      行状态 IsDeleted|DeleteBy|DeleteTime = $row"
case "$row" in
  1\|*\|* ) ok "字典为软删除：行保留，IsDeleted=1，删除人/时间已记录" ;;
  * ) bad "字典不是软删除（期望 1|...|...，实际 $row）" ;;
esac
item_row=$(docker exec -i -e MYSQL_PWD="$MYSQL_PWD_" yzh-mysql mysql -uroot -N -B \
  yzh_cert_platform -e "SELECT IsDeleted FROM Sys_DictionaryList WHERE Code='$ITEM_CODE';" 2>/dev/null)
[ "$item_row" = "1" ] && ok "字典项为软删除：行保留，IsDeleted=1" || bad "字典项不是软删除（实际 $item_row）"

# ============================================================
sec "10. 清理测试数据（物理删除，不留残留）"
# ============================================================
docker exec -i -e MYSQL_PWD="$MYSQL_PWD_" yzh-mysql mysql -uroot yzh_cert_platform \
  -e "DELETE FROM Sys_DictionaryList WHERE Code='$ITEM_CODE';
      DELETE FROM Sys_Dictionary WHERE Code='$NEW_CODE';" 2>/dev/null
left=$(docker exec -i -e MYSQL_PWD="$MYSQL_PWD_" yzh-mysql mysql -uroot -N -B \
  yzh_cert_platform -e "SELECT (SELECT COUNT(*) FROM Sys_Dictionary WHERE Code='$NEW_CODE')+(SELECT COUNT(*) FROM Sys_DictionaryList WHERE Code='$ITEM_CODE');" 2>/dev/null)
[ "$left" = "0" ] && ok "测试数据已清理干净" || bad "仍有残留（$left 行）"

# ============================================================
echo ""
echo "========================================="
echo "  测试结果：通过 $PASS ，失败 $FAIL"
echo "========================================="
[ "$FAIL" -eq 0 ] || exit 1
