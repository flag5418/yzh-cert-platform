#!/bin/bash

# AI 分析 API 直接测试脚本
# 使用已知的文件 ID 进行测试

TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiIxIiwiaWF0IjoiMTc4NjYxMjAwNyIsIm5iZiI6IjE3ODY2MTIwMDciLCJleHAiOiIxNzg2NjE5MjA3IiwiaXNzIjoidm9sLmNvcmUub3duZXIiLCJhdWQiOiJ2b2wuY29yZSJ9.gtfyUySwqy_DSguwVkYHMoIhczQcr1jNO8cKnRbrrGM"
API_BASE="http://localhost:9992"

echo "========== AI 分析 API 直接测试 =========="
echo ""

# 使用文件 ID 1 进行测试（假设存在）
FILE_ID="1"
TEMPLATE_ID="1"

echo "1. 测试 AI 分析 API..."
echo "   文件 ID: $FILE_ID"
echo "   模板 ID: $TEMPLATE_ID"
echo ""

RESPONSE=$(curl -s --url "$API_BASE/api/DocExtractionRule/analyze" \
  --header "Authorization: Bearer $TOKEN" \
  --header "Content-Type: application/json" \
  --data "{\"fileId\":$FILE_ID,\"templateId\":$TEMPLATE_ID}")

echo "响应结果:"
echo "$RESPONSE" | jq . 2>/dev/null || echo "$RESPONSE"
echo ""

# 检查是否成功
if echo "$RESPONSE" | grep -q '"status":true'; then
    echo "✅ AI 分析 API 调用成功"
elif echo "$RESPONSE" | grep -q '未配置\|未注册\|未找到'; then
    echo "❌ AI 服务配置问题: $(echo "$RESPONSE" | grep -o '"message":"[^"]*"' | head -1)"
else
    echo "⚠️ 响应异常，请检查日志"
fi
