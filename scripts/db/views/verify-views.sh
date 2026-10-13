#!/bin/bash
# ============================================================
# 视图 DDL 一致性校验
#
# 目的：防止「库里有视图、scripts/db/views/ 缺 DDL 文件」这类偏差复发
#       （2026-10-13 实测发现 7 个视图中 3 个缺档，见 26-数据库视图设计规范-V1 §一）。
#
# 检查项：
#   ① 库中每个视图都有同名 DDL 文件（否则视图重建无据可依）
#   ② scripts/db/views/ 每个 DDL 文件都对应库中视图（否则是孤儿文件/已废弃）
#   ③ DDL 内不得出现「固化 IsValid」反模式（WHERE 子句里的 `IsValid = 1`）
#      —— 规范 §三：IsValid 一律以列暴露，⛔ 不固化
#
# 用法：
#   bash scripts/db/views/verify-views.sh
#
# 退出码：0 = 全部通过；1 = 存在偏差
# ============================================================

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DB_CONTAINER="${DB_CONTAINER:-yzh-mysql}"
DB_USER="${DB_USER:-root}"
DB_PASS="${DB_PASS:-Yzh123456.}"
DB_NAME="${DB_NAME:-yzh_cert_platform}"

fail=0

# ---- ① / ② 视图 ↔ DDL 文件 双向比对 ----
db_views=$(docker exec "$DB_CONTAINER" mysql -u"$DB_USER" -p"$DB_PASS" --default-character-set=utf8mb4 -r -N -B \
  -e "SELECT TABLE_NAME FROM information_schema.VIEWS WHERE TABLE_SCHEMA='$DB_NAME' ORDER BY TABLE_NAME;" 2>/dev/null | tail -n +1 | sort)

file_views=$(ls "$SCRIPT_DIR"/*.sql 2>/dev/null | xargs -n1 basename 2>/dev/null | sed 's/\.sql$//' | sort)

echo "== 库中视图（${DB_NAME}）： $(echo "$db_views" | grep -c . ) 个 =="
echo "== DDL 文件：$(echo "$file_views" | grep -c .) 个 =="
echo

missing=$(comm -23 <(echo "$db_views") <(echo "$file_views"))
orphan=$(comm -13 <(echo "$db_views") <(echo "$file_views"))

if [ -n "$missing" ]; then
  echo "❌ 缺 DDL 文件（库中有、目录没有）："
  echo "$missing" | sed 's/^/   - /'
  fail=1
else
  echo "✅ ① 库中每个视图都有 DDL 文件"
fi

if [ -n "$orphan" ]; then
  echo "❌ 孤儿 DDL 文件（目录有、库中没有）："
  echo "$orphan" | sed 's/^/   - /'
  fail=1
else
  echo "✅ ② 无孤儿 DDL 文件"
fi

echo

# ---- ③ 固化 IsValid 反模式扫描 ----
# 只扫「WHERE 子句里出现 `IsValid = 1`」；列暴露（AS IsValid / IsValid,）不算。
hits=$(grep -nE 'IsValid[[:space:]]*=[[:space:]]*1' "$SCRIPT_DIR"/*.sql 2>/dev/null \
  | grep -vE ':[0-9]+:[[:space:]]*--' || true)

if [ -n "$hits" ]; then
  echo "❌ ③ 检测到「固化 IsValid」反模式（规范 §三：IsValid 一律以列暴露，⛔ 不固化）："
  echo "$hits" | sed 's/^/   /'
  fail=1
else
  echo "✅ ③ 未检测到「固化 IsValid」反模式"
fi

echo
if [ "$fail" -eq 0 ]; then
  echo "🎉 视图 DDL 一致性校验通过"
else
  echo "⚠️ 视图 DDL 一致性校验未通过 —— 修复后重跑（详见 26-数据库视图设计规范-V1）"
fi

exit "$fail"
