#!/bin/bash
# ============================================================
# 菜单路由契约快照 · 同步（★ 只读 DB，写快照文件）
#
# 用途：把 Sys_Menu 的 (Tag, Code, Url) 导出为 menu-urls.tsv，
#       供前端架构守卫 R12 做「路由 ↔ 菜单」双向差集校验。
#
# 用法：./sync_menu_urls.sh
# 产出：scripts/db/verify/menu-urls.tsv（须随代码提交）
#
# ⚠️ 职责边界（scripts/README.md 铁律 B1–B7）：
#      本脚本是**编排层** —— 只负责「调 mysql、落文件、报结果」。
#      SQL 一律外置在同目录 export_menu_urls.sql（铁律 B3，禁止内嵌）。
# ⚠️ 菜单变更后必须重跑本脚本，否则守卫基于过期快照。
# 维护人：脚本规范 V1（2026-09-24）
# ============================================================
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SQL_FILE="$HERE/export_menu_urls.sql"
OUT_FILE="$HERE/menu-urls.tsv"

[ -f "$SQL_FILE" ] || { echo "✗ 缺少 SQL 文件：${SQL_FILE}" >&2; exit 1; }

DB="${YZH_DB_NAME:-yzh_cert_platform}"
MYSQL_PWD_ROOT="${MYSQL_PWD_ROOT:-Yzh123456.}"
MYSQL_CONTAINER="${YZH_MYSQL_CONTAINER:-yzh-mysql}"

docker exec -i -e MYSQL_PWD="$MYSQL_PWD_ROOT" "$MYSQL_CONTAINER" \
  mysql -uroot -N -B -r --default-character-set=utf8mb4 "$DB" \
  < "$SQL_FILE" > "$OUT_FILE"

ROWS="$(grep -c -v '^#' "$OUT_FILE" || true)"
# ⚠️ 变量名后紧跟中文全角字符时，必须写 ${VAR} —— 否则 bash 会把
#    多字节字符的首字节并入变量名，set -u 下报 "unbound variable"。
echo "✓ 已同步菜单快照：${OUT_FILE}（${ROWS} 条菜单）"
echo "  下一步：cd src/certplatform-web && node scripts/guards.mjs"
