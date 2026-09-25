#!/bin/bash
# ============================================================
# 认证阶段定义 — 数据库迁移（一次性脚本，保留备查）
#
# ⚠️ 历史脚本：`cert_phase_definition_setup.sql` 是一次性迁移，
#    当前库**已应用完毕**。保留用于「从零重建历史环境」时备查。
#
# ★ 合规（scripts/README.md 铁律 B1–B7）：
#    B3 SQL 外置 → 验证查询移到 verify/verify_phase_migration.sql
#    B5 自定位   → docker 由 PATH 解析，无硬编码绝对路径 / socket 路径
#    B1 单一职责 → 不再顺带启动容器（启动归 docker/start.sh）
#
# 用法：./run_phase_migration.sh   （前置：MySQL 容器已启动）
# 维护人：脚本规范 V1（2026-09-24）
# ============================================================
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SETUP_SQL="$HERE/cert_phase_definition_setup.sql"
VERIFY_SQL="$HERE/verify/verify_phase_migration.sql"

DB="${YZH_DB_NAME:-yzh_cert_platform}"
MYSQL_CONTAINER="${YZH_MYSQL_CONTAINER:-yzh-mysql}"
MYSQL_PWD_ROOT="${MYSQL_PWD_ROOT:-Yzh123456.}"

[ -f "$SETUP_SQL" ]  || { echo "✗ 缺少 SQL 文件：${SETUP_SQL}"  >&2; exit 1; }
[ -f "$VERIFY_SQL" ] || { echo "✗ 缺少 SQL 文件：${VERIFY_SQL}" >&2; exit 1; }

echo "== 1. 等待 MySQL 就绪 =="
READY=0
for _ in $(seq 1 30); do
  if docker exec "$MYSQL_CONTAINER" mysqladmin ping -h localhost --silent 2>/dev/null; then
    READY=1; echo "   MySQL 已就绪"; break
  fi
  printf '.'; sleep 1
done
[ "$READY" = "1" ] || { echo "" >&2; echo "✗ MySQL 未就绪（容器 ${MYSQL_CONTAINER} 未启动？先跑 docker/start.sh）" >&2; exit 1; }

echo "== 2. 执行迁移 SQL =="
docker exec -i -e MYSQL_PWD="$MYSQL_PWD_ROOT" "$MYSQL_CONTAINER" \
  mysql -uroot --default-character-set=utf8mb4 "$DB" < "$SETUP_SQL"

echo "== 3. 验证（只读，SQL 见 verify/verify_phase_migration.sql） =="
docker exec -i -e MYSQL_PWD="$MYSQL_PWD_ROOT" "$MYSQL_CONTAINER" \
  mysql -uroot -N -B --default-character-set=utf8mb4 "$DB" < "$VERIFY_SQL"

echo ""
echo "✓ 认证阶段定义迁移完成"
