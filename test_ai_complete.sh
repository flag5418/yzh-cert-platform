#!/bin/bash

# 完整的 AI 分析测试脚本
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiIxIiwiaWF0IjoiMTc4NjYxODg4NSIsIm5iZiI6IjE3ODY2MTg4ODUiLCJleHAiOiIxNzg2NjI2MDg1IiwiaXNzIjoidm9sLmNvcmUub3duZXIiLCJhdWQiOiJ2b2wuY29yZSJ9.EoGgl8wmSiFpynBACRXMa82jpI1wP_7zPsr8dNWxe1I"
API_BASE="http://localhost:9992"

echo "========== 完整 AI 分析测试 =========="
echo ""

# 1. 获取提示词模板列表
echo "1. 获取提示词模板列表..."
TEMPLATE_RESPONSE=$(curl -s --url "$API_BASE/api/prompt-template/list" \
  --header "Authorization: Bearer $TOKEN")

echo "模板列表:"
echo "$TEMPLATE_RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); data=d.get('data',[]); [print(f'  ID={t.get(\"id\")}, Code={t.get(\"PromptCode\")}, Type={t.get(\"PromptType\")}') for t in data]"
echo ""

# 2. 尝试使用正确的 fileCode 格式调用 AI 分析
# 注意：fileCode 格式应该是 FL-{FolderCode}|{FileName}|{Type}
# 阶段编码应该是 STAGE-01 而不是 STAGE01

echo "2. 测试 AI 分析 API..."
echo "   注意：当前数据库中可能没有文件记录"
echo ""

# 使用一个可能存在的 fileCode 进行测试
# 格式：FL-FD-SDC-{标准编码}|{阶段编码}|L{层级}|S{序号}|{文件名}|{扩展名}
TEST_FILECODE="FL-FD-SDC-ISO134852016|STAGE-01|L02|S001|附录三 程序文件清单.doc"

echo "   测试 fileCode: $TEST_FILECODE"
AI_RESPONSE=$(curl -s --url "$API_BASE/api/DocExtractionRule/analyze" \
  --header "Authorization: Bearer $TOKEN" \
  --header "Content-Type: application/json" \
  --data "{\"fileCode\":\"$TEST_FILECODE\",\"skill\":\"word\"}")

echo "   AI 分析响应:"
echo "$AI_RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$AI_RESPONSE"
echo ""

# 3. 分析结果
echo "3. 分析结果..."
if echo "$AI_RESPONSE" | grep -q "文件不存在"; then
    echo "   ❌ 文件不存在 - 数据库中没有该 fileCode 对应的记录"
    echo ""
    echo "   可能的原因："
    echo "   1. 文件尚未上传到系统中"
    echo "   2. fileCode 格式不正确"
    echo "   3. 文件记录被删除"
    echo ""
    echo "   解决方案："
    echo "   - 通过前端页面上传文件到目录中"
    echo "   - 或者检查数据库中实际的 fileCode 值"
elif echo "$AI_RESPONSE" | grep -q "文档内容为空或提取失败"; then
    echo "   ❌ 文档内容提取失败 - 文件存在但无法提取内容"
    echo ""
    echo "   可能的原因："
    echo "   1. 文件尚未完成格式转换（.doc → .docx）"
    echo "   2. 文件损坏或格式不支持"
    echo "   3. MinIO 中文件不存在"
elif echo "$AI_RESPONSE" | grep -q '"Fields"'; then
    echo "   ✅ AI 分析成功"
else
    echo "   ⚠️ 未知响应"
fi
