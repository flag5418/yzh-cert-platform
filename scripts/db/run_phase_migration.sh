#!/bin/bash
##############################################################################
# 认证阶段定义 — 数据库迁移脚本
# 执行：./run_phase_migration.sh
##############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
DOCKER_BIN="/Applications/OrbStack.app/Contents/MacOS/xbin/docker"
DOCKER_SOCKET="/Volumes/Expand/wangqingquan/.orbstack/run/docker.sock"

echo "========================================="
echo "  认证阶段定义 — 数据库迁移"
echo "========================================="

# 设置 Docker socket 路径
export DOCKER_HOST="unix://$DOCKER_SOCKET"

# 检查 Docker/OrbStack 是否运行
if ! "$DOCKER_BIN" info &>/dev/null; then
    echo "[ERROR] OrbStack 未运行，请先启动 OrbStack"
    exit 1
fi

# 检查 MySQL 容器是否运行
if ! "$DOCKER_BIN" ps --format "{{.Names}}" 2>/dev/null | grep -q yzh-mysql; then
    echo "[INFO] 启动 MySQL 容器..."
    cd "$PROJECT_DIR/docker"
    "$DOCKER_BIN" compose -f "$PROJECT_DIR/docker/compose.yml" up -d mysql
    sleep 5
fi

# 等待 MySQL 就绪
echo "[INFO] 等待 MySQL 就绪..."
for i in $(seq 1 30); do
    if "$DOCKER_BIN" exec yzh-mysql mysqladmin ping -h localhost --silent 2>/dev/null; then
        echo "[OK] MySQL 已就绪"
        break
    fi
    echo -n "."
    sleep 1
done

# 执行 SQL
echo "[INFO] 执行 SQL 脚本..."
"$DOCKER_BIN" exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform < "$SCRIPT_DIR/cert_phase_definition_setup.sql"

if [ $? -eq 0 ]; then
    echo "[OK] SQL 执行成功"
else
    echo "[WARN] SQL 执行有警告，请检查输出"
fi

# 验证
echo ""
echo "[INFO] 验证数据..."
"$DOCKER_BIN" exec yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform -e "
SELECT phase_code, phase_name, sequence_order, is_valid 
FROM v_cert_phase_definition 
ORDER BY sequence_order;
" 2>/dev/null

echo ""
echo "[SUCCESS] 数据库迁移完成"
echo ""
echo "下一步：编译并启动后端服务"
echo "  cd $PROJECT_DIR/src/certplatform-api"
echo "  dotnet build Cert.Platform.sln"
echo "  ./scripts/backend/restart-backend.sh"
