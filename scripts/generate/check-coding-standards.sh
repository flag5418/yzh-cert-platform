#!/bin/bash
# 代码规范检查脚本

echo "========================================"
echo "代码规范检查"
echo "========================================"

# 后端检查
echo ""
echo "【后端检查】"

# 1. 检查超大Service
echo "1. 检查超大Service (>500行):"
find src/server/Vue.NetCore/vol.api/VOL.Builder/Services -name "*.cs" -exec wc -l {} \; | sort -nr | head -10 | awk '$1 > 500 {print "  ⚠️  "$2": "$1"行"}'

# 2. 检查空catch块
echo "2. 检查空catch块:"
grep -rn "catch\s*{" src/server/Vue.NetCore/vol.api/VOL.Builder/ --include="*.cs" | grep -v "bin\|obj" | while read line; do
    echo "  ❌ $line"
done

# 3. 检查静态Instance
echo "3. 检查静态Instance属性:"
grep -rn "public static.*Instance" src/server/Vue.NetCore/vol.api/VOL.Builder/ --include="*.cs" | grep -v "bin\|obj" | while read line; do
    echo "  ⚠️ $line"
done

# 前端检查
echo ""
echo "【前端检查】"

# 4. 检查大组件
echo "4. 检查大组件 (>500行):"
find src/server/Vue.NetCore/vol.web/src/views/cert -name "*.vue" -exec wc -l {} \; | sort -nr | head -10 | awk '$1 > 500 {print "  ⚠️  "$2": "$1"行"}'

# 5. 检查any类型
echo "5. 检查any类型使用:"
grep -rn ": any" src/server/Vue.NetCore/vol.web/src/views/cert --include="*.vue" --include="*.ts" | grep -v "node_modules" | head -5 | while read line; do
    echo "  ⚠️ $line"
done

echo ""
echo "========================================"
echo "检查完成"
echo "========================================"
