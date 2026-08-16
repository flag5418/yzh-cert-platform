#!/bin/bash

# 快速重启后端服务
# 流程：按进程名过滤 dotnet/VOL.WebApi 直接停止 → 编译 → 后台启动
# 用法: ./restart-backend.sh

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
print_error() { echo -e "${RED}[ERROR]${NC} $1"; }

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "========================================="
echo "    快速重启后端服务"
echo "========================================="

# 1. 停止已运行的服务（按进程名过滤，脚本自身退出码不影响流程）
print_info "停止已运行的服务..."
"$SCRIPT_DIR/stop-backend.sh"
sleep 1

# 2. 重新编译并后台启动
print_info "重新编译并启动服务..."
"$SCRIPT_DIR/run-backend.sh" all

if [ $? -eq 0 ]; then
    echo ""
    print_info "重启完成: http://localhost:9992"
else
    echo ""
    print_error "重启失败，请查看上方错误信息"
    exit 1
fi
