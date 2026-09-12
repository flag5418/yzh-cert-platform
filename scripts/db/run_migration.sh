#!/bin/bash
# 认证阶段定义数据库迁移脚本

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "========================================="
echo "  认证阶段定义 — 数据库迁移"
echo "========================================="

# 检查 Docker 是否运行
if ! docker info &>/dev/null; then
    echo "[ERROR] Docker 未运行，请先启动 OrbStack"
    exit 1
fi

# 检查 MySQL 容器是否运行
if ! docker ps --format "{{.Names}}" | grep -q yzh-mysql; then
    echo "[INFO] 启动 MySQL 容器..."
    cd "$PROJECT_DIR/docker"
    docker compose up -d mysql
    sleep 5
fi

# 等待 MySQL 就绪
echo "[INFO] 等待 MySQL 就绪..."
for i in $(seq 1 30); do
    if docker exec yzh-mysql mysqladmin ping -h localhost --silent 2>/dev/null; then
        echo "[OK] MySQL 已就绪"
        break
    fi
    echo -n "."
    sleep 1
done

# 执行 SQL
echo "[INFO] 执行 SQL 脚本..."
docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform < "$SCRIPT_DIR/cert_phase_definition_setup.sql"

if [ $? -eq 0 ]; then
    echo "[OK] SQL 执行成功"
else
    echo "[WARN] SQL 执行有警告，请检查输出"
fi

# 验证
echo ""
echo "[INFO] 验证数据..."
docker exec yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform -e "
SELECT phase_code, phase_name, sequence_order, is_valid 
FROM v_cert_phase_definition 
ORDER BY sequence_order;
" 2>/dev/null

echo ""
echo "[SUCCESS] 数据库迁移完成"
