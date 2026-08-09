#!/bin/bash

# 停止后端服务

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

echo "========================================="
echo "    停止后端服务"
echo "========================================="
echo ""

if lsof -Pi :$PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
    print_info "正在停止端口 $PORT 上的服务..."
    kill $(lsof -t -i:$PORT) 2>/dev/null
    sleep 1
    
    if lsof -Pi :$PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
        print_error "服务仍在运行，尝试强制终止..."
        kill -9 $(lsof -t -i:$PORT) 2>/dev/null
        sleep 1
    fi
    
    print_info "服务已停止"
else
    print_info "端口 $PORT 上没有运行中的服务"
fi
