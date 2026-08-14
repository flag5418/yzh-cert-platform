#!/bin/bash

# AI 分析 API 测试脚本 V2
# 使用新的 Token 测试 DocExtractionRule 的 AI 分析功能

BASE_URL="http://localhost:9992"
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiIxIiwiaWF0IjoiMTc4NjYxMjAwNyIsIm5iZiI6IjE3ODY2MTIwMDciLCJleHAiOiIxNzg2NjE5MjA3IiwiaXNzIjoidm9sLmNvcmUub3duZXIiLCJhdWQiOiJ2b2wuY29yZSJ9.gtfyUySwqy_DSguwVkYHMoIhczQcr1jNO8cKnRbrrGM"

echo "========== AI 分析 API 测试 (V2) =========="
echo ""

# 1. 获取文件列表
echo "1. 获取文件列表..."
FILES_RESPONSE=$(curl -s -X POST "${BASE_URL}/api/standarddirectory/getPageData" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"page":1,"rows":5}')
echo "$FILES_RESPONSE" | jq '.data.rows[] | {fileCode: .fileCode, fileName: .fileName, fileType: .fileType, convertStatus: .convertStatus}'
echo ""

# 提取第一个文件的 fileCode
FILE_CODE=$(echo "$FILES_RESPONSE" | jq -r '.data.rows[0].fileCode // empty')
if [ -z "$FILE_CODE" ]; then
    echo "❌ 未获取到文件列表，请确认有上传的文件"
    exit 1
fi
echo "✓ 获取到 fileCode: $FILE_CODE"
echo ""

# 2. 测试 AI 分析 API
echo "2. 测试 AI 分析 API (Word)..."
curl -s -X POST "${BASE_URL}/api/doc-extraction-rule/analyze" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"fileCode\":\"${FILE_CODE}\",\"skill\":\"word\"}" | jq '.'
echo ""

echo "========== 测试完成 =========="
