#!/bin/bash
# ============================================================
# 数据字典 · 测试夹具清理（★ 会写库，必须显式调用）
#
# 用途：API 端到端测试会创建 `__T__` 前缀的临时数据（软删除后行仍保留），
#       本脚本负责**物理删除**这些测试残留，使测试可重复运行。
#
# 归属：测试夹具（fixture），不是业务数据清理 —— 只删 `__T__` 前缀行。
# ⛔ 安全约束：Code 必须以 `__T__` 开头，否则拒绝执行（防止误删业务数据）。
#
# 用法：./cleanup_dict_test_data.sh <DictCode> <ItemCode>
# 退出码：0 = 已清理且残留为 0；1 = 仍有残留
# 维护人：脚本规范 V1（2026-09-24）
# ============================================================
set -euo pipefail

[ $# -eq 2 ] || { echo "用法: $0 <DictCode> <ItemCode>" >&2; exit 2; }
DICT_CODE="$1"
ITEM_CODE="$2"

case "$DICT_CODE$ITEM_CODE" in
  __T__*__T__*|__T__*) ;;
  *) echo "⛔ 拒绝执行：Code 必须以 __T__ 开头（防误删业务数据）：$DICT_CODE / $ITEM_CODE" >&2; exit 2 ;;
esac

HERE="$(cd "$(dirname "$0")" && pwd)"
SQL_FILE="$HERE/cleanup_dict_test_data.sql"
[ -f "$SQL_FILE" ] || { echo "✗ 缺少 SQL 文件：${SQL_FILE}" >&2; exit 1; }

DB="${YZH_DB_NAME:-yzh_cert_platform}"
MYSQL_PWD_ROOT="${MYSQL_PWD_ROOT:-Yzh123456.}"
MYSQL=(docker exec -i -e MYSQL_PWD="$MYSQL_PWD_ROOT" yzh-mysql mysql -uroot)

# SQL 外置（铁律 B3）：删除 + 残留计数都在 .sql 里，脚本只注入参数并取回计数。
left=$(sed -e "s|__DICT_CODE__|${DICT_CODE}|g" -e "s|__ITEM_CODE__|${ITEM_CODE}|g" "$SQL_FILE" \
  | "${MYSQL[@]}" -N -B "$DB" | tail -1)

echo "$left"
[ "$left" = "0" ]
