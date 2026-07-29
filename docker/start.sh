#!/bin/bash
##############################################################################
# 映智汇认证审核管理系统 - Docker 服务启动脚本
##############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  映智汇认证审核管理系统 - 开发环境启动${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# 检查 Docker 是否可用
if ! docker info &>/dev/null; then
    echo -e "${YELLOW}等待 Docker 就绪...${NC}"
    sleep 5
fi

echo -e "${GREEN}[1/2] 启动 MySQL 8.0 (端口 3307)...${NC}"
docker compose up -d mysql

echo -e "${GREEN}[2/2] 启动 Redis 7 (端口 6380)...${NC}"
docker compose up -d redis

# 等待就绪
echo ""
echo -e "${YELLOW}等待服务就绪...${NC}"
sleep 5

# 状态
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  服务状态${NC}"
echo -e "${GREEN}========================================${NC}"
docker compose ps
echo ""

echo "MySQL:   mysql -h 127.0.0.1 -P 3307 -u root -pYzh123456. yzh_cert_platform"
echo "Redis:   redis-cli -p 6380"
echo ""
