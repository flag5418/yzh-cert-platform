#!/bin/bash
##############################################################################
# 映智汇认证审核管理系统 - Docker 服务停止脚本
##############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

GREEN='\033[0;32m'
NC='\033[0m'

echo -e "${GREEN}停止 yzh-mysql / yzh-redis ...${NC}"
docker compose down

echo ""
echo -e "${GREEN}所有服务已停止${NC}"
