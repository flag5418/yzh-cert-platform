#!/bin/bash

# AI 分析 API 测试脚本
# 测试 DocExtractionRule 的 AI 分析功能

BASE_URL="http://localhost:9992"
# 使用用户提供的有效 Token
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiIxIiwiaWF0IjoiMTc4NjYwNjI5NCIsIm5iZiI6IjE3ODY2MDYyOTQiLCJleHAiOiIxNzg2NjEzNDk0IiwiaXNzIjoidm9sLmNvcmUub3duZXIiLCJhdWQiOiJ2b2wuY29yZSJ9.jcPG2gHfjMMYyiU2RY8PBUqLP0ErMSiy4hNHErVASsU"

echo "========== AI 分析 API 测试 =========="
echo ""

# 1. 首先测试 SysConfig API 确认 AI 配置
echo "1. 测试 SysConfig API (ai_model 配置)..."
curl -s -X POST "${BASE_URL}/api/sys-config/list" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"category":"ai_model"}' | jq '.'
echo ""
echo ""

# 2. 获取文件列表（用于获取可用的 fileCode）
echo "2. 获取文件列表..."
curl -s -X POST "${BASE_URL}/api/standarddirectory/getPageData" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"page":1,"rows":5,"sort":"Id","order":"desc","wheres":[]}' | jq '.data.rows[0] | {fileCode: .fileCode, fileName: .fileName, fileType: .fileType}'
echo ""
echo ""

# 3. 测试 AI 分析 API（需要替换为实际的 fileCode）
echo "3. 测试 AI 分析 API..."
echo "请从上面的文件列表中获取一个 fileCode，然后执行："
echo ""
echo "curl -X POST '${BASE_URL}/api/doc-extraction-rule/analyze' \\"
echo "  -H 'Authorization: Bearer ${TOKEN}' \\"
echo "  -H 'Content-Type: application/json' \\"
echo "  -d '{\"fileCode\":\"<FILE_CODE>\",\"skill\":\"word\"}' | jq '.'"
echo ""

# 4. 获取技能列表
echo "4. 获取技能列表..."
curl -s -X GET "${BASE_URL}/api/doc-extraction-rule/skills" \
  -H "Authorization: Bearer ${TOKEN}" | jq '.'
echo ""

# 5. 获取提示词模板列表
echo "5. 获取提示词模板列表..."
curl -s -X POST "${BASE_URL}/api/prompt-template/getPageData" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"page":1,"rows":10}' | jq '.data.rows[] | {promptCode: .promptCode, name: .name, version: .version, isActive: .isActive}'
echo ""

echo "========== 测试完成 =========="
