#!/bin/bash
##############################################################################
# 映智汇认证审核管理系统 - Docker 服务状态查看脚本
##############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  Docker 服务状态${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# 检查 Docker 是否可用
if ! docker info &>/dev/null; then
    echo -e "${RED}错误: Docker 未运行，请先启动 OrbStack${NC}"
    exit 1
fi

# 显示容器状态
echo -e "${CYAN}容器状态:${NC}"
docker compose ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}"
echo ""

# 显示资源使用
echo -e "${CYAN}资源使用:${NC}"
docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}\t{{.BlockIO}}" yzh-mysql yzh-redis yzh-minio 2>/dev/null || echo "  容器未运行"
echo ""

# 显示连接信息
echo -e "${CYAN}连接信息:${NC}"
echo "  MySQL:    mysql -h 127.0.0.1 -P 3307 -u root -p"
echo "  Redis:    redis-cli -p 6380"
echo "  MinIO:    http://127.0.0.1:9001 (Console)"
echo "            http://127.0.0.1:9000 (API)"
echo ""
