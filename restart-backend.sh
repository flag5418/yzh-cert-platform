#!/bin/bash

# 快速重启后端服务
# 先停止已运行的服务，再重新编译和启动

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

PORT=9991

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 停止已运行的服务
print_info "停止已运行的服务..."
if lsof -Pi :$PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
    kill $(lsof -t -i:$PORT) 2>/dev/null
    sleep 1
    print_info "服务已停止"
else
    print_info "没有运行中的服务"
fi

# 重新编译并运行
print_info "重新编译并启动服务..."
./run-backend.sh all
