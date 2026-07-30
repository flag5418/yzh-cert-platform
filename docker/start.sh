#!/bin/bash
##############################################################################
# 映智汇认证审核管理系统 - Docker 服务启动脚本
# 仅启动本项目所需的 MySQL + Redis + MinIO，不影响其他项目
##############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  映智汇认证审核管理系统 - 开发环境启动${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# 检查 Docker 是否可用
if ! docker info &>/dev/null; then
    echo -e "${RED}错误: Docker 未运行，请先启动 OrbStack${NC}"
    exit 1
fi

# 检查端口是否被占用（排除本项目容器）
check_port() {
    local port=$1
    local name=$2
    local pid=$(lsof -ti :"$port" 2>/dev/null || true)
    if [ -n "$pid" ]; then
        local container=$(docker ps --filter "publish=$port" --format "{{.Names}}" 2>/dev/null || true)
        if [ -n "$container" ]; then
            echo -e "${RED}端口 $port 被容器 [$container] 占用，无法启动 $name${NC}"
            echo -e "${YELLOW}提示: 可执行 docker stop $container 停止占用容器${NC}"
        else
            echo -e "${RED}端口 $port 被本机进程 (PID: $pid) 占用，无法启动 $name${NC}"
        fi
        return 1
    fi
    return 0
}

# 端口冲突检测
HAS_CONFLICT=0
for pair in "3307:MySQL" "6380:Redis" "9000:MinIO-API" "9001:MinIO-Console"; do
    port="${pair%%:*}"
    name="${pair##*:}"
    if ! check_port "$port" "$name"; then
        HAS_CONFLICT=1
    fi
done

if [ "$HAS_CONFLICT" -eq 1 ]; then
    echo ""
    echo -e "${RED}存在端口冲突，请先解决后再启动${NC}"
    exit 1
fi

# 加载环境变量
if [ -f .env ]; then
    source .env
    echo -e "${CYAN}已加载 .env 配置${NC}"
else
    echo -e "${YELLOW}警告: 未找到 .env 文件，使用默认占位密码。请复制 .env.example 为 .env 并填写真实密码${NC}"
fi

echo ""

# 启动服务
echo -e "${GREEN}[1/3] 启动 MySQL 8.0 (端口 3307)...${NC}"
docker compose up -d mysql

echo -e "${GREEN}[2/3] 启动 Redis 7 (端口 6380)...${NC}"
docker compose up -d redis

echo -e "${GREEN}[3/3] 启动 MinIO (端口 9000/9001)...${NC}"
docker compose up -d minio

# 等待就绪
echo ""
echo -e "${YELLOW}等待服务就绪...${NC}"

# 等待 MySQL 就绪
echo -n "  MySQL"
for i in $(seq 1 15); do
    if docker exec yzh-mysql mysqladmin ping -h localhost --silent 2>/dev/null; then
        echo -e " ${GREEN}✓${NC}"
        break
    fi
    echo -n "."
    sleep 1
    [ "$i" -eq 15 ] && echo -e " ${RED}超时${NC}"
done

# 等待 Redis 就绪
echo -n "  Redis"
for i in $(seq 1 10); do
    if docker exec yzh-redis redis-cli ping 2>/dev/null | grep -q PONG; then
        echo -e " ${GREEN}✓${NC}"
        break
    fi
    echo -n "."
    sleep 1
    [ "$i" -eq 10 ] && echo -e " ${RED}超时${NC}"
done

# 等待 MinIO 就绪
echo -n "  MinIO"
for i in $(seq 1 10); do
    if docker exec yzh-minio curl -sf http://localhost:9000/minio/health/live 2>/dev/null; then
        echo -e " ${GREEN}✓${NC}"
        break
    fi
    echo -n "."
    sleep 1
    [ "$i" -eq 10 ] && echo -e " ${RED}超时${NC}"
done

# 状态
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  服务状态${NC}"
echo -e "${GREEN}========================================${NC}"
docker compose ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}"
echo ""
echo -e "${CYAN}MySQL:    mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform"
echo -e "${CYAN}Redis:    redis-cli -p 6380"
echo -e "${CYAN}MinIO:    http://127.0.0.1:9001 (Console) / http://127.0.0.1:9000 (API)${NC}"
echo ""
