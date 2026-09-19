#!/bin/bash
# ============================================================
# YZH 架构冻结检查脚本
# 用途：检查后台管理是否符合 YZH 架构规范
# 使用：bash scripts/tools/freeze-check.sh
# ============================================================

set -e

ADMIN_DIR="src/certplatform-web/cert/cert-admin/src"
SHARE_DIR="src/certplatform-web/cert/cert-share/src"
PASS_COUNT=0
FAIL_COUNT=0
WARN_COUNT=0

echo "=========================================="
echo "  YZH 架构冻结检查"
echo "=========================================="
echo ""

# 1. Vol 遗留检查
echo "--- 1. Vol 遗留检查 ---"

# 1.1 getPageData 检查（排除已标记为 TODO/占位/废弃的文件）
# 先获取所有包含 getPageData 的文件
getPageData_files=$(grep -rl "getPageData" "$ADMIN_DIR/" "$SHARE_DIR/" 2>/dev/null || true)
getPageData_unmarked=""

for file in $getPageData_files; do
    # 检查文件中是否有 TODO/@deprecated/占位/待修复 标记
    if ! grep -q -E "(TODO:|@deprecated|占位|待修复)" "$file" 2>/dev/null; then
        getPageData_unmarked="$getPageData_unmarked $file"
    fi
done

if [ -n "$getPageData_unmarked" ]; then
    echo "❌ FAIL: 存在未标记的 getPageData 调用"
    for file in $getPageData_unmarked; do
        echo "  $file"
    done
    FAIL_COUNT=$((FAIL_COUNT + 1))
else
    echo "✅ PASS: 无未标记的 getPageData 调用"
    PASS_COUNT=$((PASS_COUNT + 1))
fi

# 1.2 http.ts 检查
if grep -r "utils/http" "$ADMIN_DIR/" "$SHARE_DIR/" 2>/dev/null | grep -v "// " | grep -v "TODO"; then
    echo "❌ FAIL: 存在 http.ts 依赖"
    FAIL_COUNT=$((FAIL_COUNT + 1))
else
    echo "✅ PASS: 无 http.ts 依赖"
    PASS_COUNT=$((PASS_COUNT + 1))
fi

# 1.3 ControllerBase 检查（仅允许 B 类名单）
if grep -r "ControllerBase" "$ADMIN_DIR/" "$SHARE_DIR/" 2>/dev/null | grep -v "// " | grep -v "TODO"; then
    echo "⚠️ WARN: 存在 ControllerBase 引用（需确认是否为 B 类模块）"
    WARN_COUNT=$((WARN_COUNT + 1))
else
    echo "✅ PASS: 无 ControllerBase 引用"
    PASS_COUNT=$((PASS_COUNT + 1))
fi

echo ""

# 2. 响应格式检查
echo "--- 2. 响应格式检查 ---"

# 2.1 {code:200} 检查
if grep -r "code: 200" "$ADMIN_DIR/api/" "$SHARE_DIR/api/" 2>/dev/null | grep -v "// " | grep -v "TODO"; then
    echo "❌ FAIL: 存在 {code:200} 响应格式"
    FAIL_COUNT=$((FAIL_COUNT + 1))
else
    echo "✅ PASS: 无 {code:200} 响应格式"
    PASS_COUNT=$((PASS_COUNT + 1))
fi

# 2.2 {code: 检查
if grep -r "{code:" "$ADMIN_DIR/api/" "$SHARE_DIR/api/" 2>/dev/null | grep -v "// " | grep -v "TODO"; then
    echo "❌ FAIL: 存在旧响应格式"
    FAIL_COUNT=$((FAIL_COUNT + 1))
else
    echo "✅ PASS: 无旧响应格式"
    PASS_COUNT=$((PASS_COUNT + 1))
fi

echo ""

# 3. 页面硬编码检查
echo "--- 3. 页面硬编码检查 ---"

# 3.1 硬编码 columns 检查
if grep -r "const columns" "$ADMIN_DIR/pages/" 2>/dev/null | grep -v "// " | grep -v "TODO" | grep -v "logic.ts"; then
    echo "⚠️ WARN: 存在硬编码 columns（需确认是否为 B 类模块）"
    WARN_COUNT=$((WARN_COUNT + 1))
else
    echo "✅ PASS: 无硬编码 columns"
    PASS_COUNT=$((PASS_COUNT + 1))
fi

# 3.2 硬编码 searchFields 检查
if grep -r "const searchFields" "$ADMIN_DIR/pages/" 2>/dev/null | grep -v "// " | grep -v "TODO" | grep -v "logic.ts"; then
    echo "⚠️ WARN: 存在硬编码 searchFields（需确认是否为 B 类模块）"
    WARN_COUNT=$((WARN_COUNT + 1))
else
    echo "✅ PASS: 无硬编码 searchFields"
    PASS_COUNT=$((PASS_COUNT + 1))
fi

echo ""

# 4. 路由完整性检查
echo "--- 4. 路由完整性检查 ---"

# 检查 pages 下的目录是否都有对应路由
PAGES_DIR="$ADMIN_DIR/pages"
ROUTES_FILE="$ADMIN_DIR/router/index.ts"

for dir in $(find "$PAGES_DIR" -mindepth 2 -maxdepth 2 -type d 2>/dev/null); do
    page_name=$(basename "$dir")
    parent_name=$(basename "$(dirname "$dir")")
    
    # 跳过特殊目录
    if [[ "$parent_name" == "pages" ]]; then
        continue
    fi
    
    # 检查路由是否存在
    if ! grep -q "$parent_name/$page_name" "$ROUTES_FILE" 2>/dev/null; then
        echo "⚠️ WARN: 页面 $parent_name/$page_name 未在路由中注册"
        WARN_COUNT=$((WARN_COUNT + 1))
    fi
done

echo "✅ PASS: 路由检查完成"
PASS_COUNT=$((PASS_COUNT + 1))

echo ""

# 5. API 文件检查
echo "--- 5. API 文件检查 ---"

# 检查 API 文件是否使用 yzhApi
for api_file in $(find "$ADMIN_DIR/api/" "$SHARE_DIR/api/" -name "*.ts" 2>/dev/null); do
    if grep -q "from '@yzh-core/api/client'" "$api_file" 2>/dev/null || grep -q "from '@yzh-core'" "$api_file" 2>/dev/null; then
        echo "✅ PASS: $(basename "$api_file") 使用 yzhApi"
    elif grep -q "from '@yzh-core/utils/http'" "$api_file" 2>/dev/null; then
        echo "❌ FAIL: $(basename "$api_file") 使用 http.ts"
        FAIL_COUNT=$((FAIL_COUNT + 1))
    else
        echo "⚠️ WARN: $(basename "$api_file") 未使用 yzhApi"
        WARN_COUNT=$((WARN_COUNT + 1))
    fi
done

echo ""

# 汇总
echo "=========================================="
echo "  检查结果汇总"
echo "=========================================="
echo "✅ PASS: $PASS_COUNT"
echo "❌ FAIL: $FAIL_COUNT"
echo "⚠️ WARN: $WARN_COUNT"
echo ""

if [ $FAIL_COUNT -gt 0 ]; then
    echo "❌ 检查未通过，请修复上述 FAIL 项后重试"
    exit 1
else
    echo "✅ 检查通过（WARN 项需人工确认）"
    exit 0
fi
