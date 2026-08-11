#!/bin/bash
##############################################################################
# 映智汇认证审核管理系统 - Docker 服务重启脚本
##############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}重启 Docker 服务...${NC}"
echo ""

# 停止服务
if [ -f ./stop.sh ]; then
    ./stop.sh
fi

# 等待一会儿
echo -e "${YELLOW}等待 3 秒...${NC}"
sleep 3

# 启动服务
if [ -f ./start.sh ]; then
    ./start.sh
fi
