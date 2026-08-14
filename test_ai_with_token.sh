#!/bin/bash

# 使用最新 token 测试 AI 分析 API
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiIxIiwiaWF0IjoiMTc4NjYxODg4NSIsIm5iZiI6IjE3ODY2MTg4ODUiLCJleHAiOiIxNzg2NjI2MDg1IiwiaXNzIjoidm9sLmNvcmUub3duZXIiLCJhdWQiOiJ2b2wuY29yZSJ9.EoGgl8wmSiFpynBACRXMa82jpI1wP_7zPsr8dNWxe1I"
API_BASE="http://localhost:9992"

echo "========== 使用最新 Token 测试 AI 分析 API =========="
echo ""

# 1. 首先查询组织树，获取机构和标准信息
echo "1. 查询组织树..."
TREE_RESPONSE=$(curl -s --url "$API_BASE/api/standard-directory/organization-tree" \
  --header "Authorization: Bearer $TOKEN")

echo "组织树响应:"
echo "$TREE_RESPONSE" | python3 -m json.tool 2>/dev/null | head -50 || echo "$TREE_RESPONSE"
echo ""

# 2. 查询第一个机构的文件列表
# 从组织树中提取第一个机构和标准
echo "2. 尝试查询文件列表..."

# 尝试多个可能的目录编码
for DIR_CODE in "SDC-ISO134852016|STAGE-01" "SDC-ISO134852016|STAGE01" "SDC-ISO90012015|STAGE-01"; do
    echo "   尝试目录编码: $DIR_CODE"
    FILES_RESPONSE=$(curl -s --url "$API_BASE/api/standard-directory/directory-files?directoryCode=$DIR_CODE" \
      --header "Authorization: Bearer $TOKEN")
    
    FILE_COUNT=$(echo "$FILES_RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d) if isinstance(d,list) else len(d.get('data',[])))" 2>/dev/null || echo "0")
    
    if [ "$FILE_COUNT" -gt 0 ]; then
        echo "   ✅ 找到 $FILE_COUNT 个文件"
        echo "$FILES_RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); data=d if isinstance(d,list) else d.get('data',[]); [print(f'   - {f.get(\"fileName\",f.get(\"FileName\"))} (code: {f.get(\"fileCode\",f.get(\"FileCode\"))}, id: {f.get(\"id\",f.get(\"Id\"))})') for f in data[:3]]"
        break
    fi
done
echo ""

# 3. 查询提示词模板
echo "3. 查询提示词模板..."
TEMPLATE_RESPONSE=$(curl -s --url "$API_BASE/api/prompt-template/getPageData" \
  --header "Authorization: Bearer $TOKEN" \
  --header "Content-Type: application/json" \
  --data '{"tableName":"cert_prompt_template","page":1,"rows":10,"sort":"Id","order":"desc","wheres":[]}')

echo "模板响应:"
echo "$TEMPLATE_RESPONSE" | python3 -m json.tool 2>/dev/null | head -50 || echo "$TEMPLATE_RESPONSE"
echo ""

# 4. 尝试调用 AI 分析 API（使用一个示例 fileCode）
echo "4. 测试 AI 分析 API..."
echo "   使用 fileCode: FL-FD-SDC-ISO134852016|STAGE01|L02|S001|附录三 程序文件清单.doc"

AI_RESPONSE=$(curl -s --url "$API_BASE/api/DocExtractionRule/analyze" \
  --header "Authorization: Bearer $TOKEN" \
  --header "Content-Type: application/json" \
  --data '{"fileCode":"FL-FD-SDC-ISO134852016|STAGE01|L02|S001|附录三 程序文件清单.doc","skill":"word"}')

echo "AI 分析响应:"
echo "$AI_RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$AI_RESPONSE"
