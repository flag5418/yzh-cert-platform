#!/bin/bash
# ============================================================
# 数据字典 · 软删除语义核对（★ 只读，不做任何修改）
#
# 用途：API 端到端测试需要核对「删除接口是否只做软删除」——
#       这是 API 副作用，必须看库才能验证，属**正当的只读核对**。
#       本脚本把这段 SQL 从测试脚本中抽出，使测试脚本不直接持有 SQL。
#
# 用法：./verify_dict_softdelete.sh <DictCode> <ItemCode>
# 输出（制表符分隔）：
#   第 1 行：字典  IsDeleted|DeleteBy|DeleteTime
#   第 2 行：字典项 IsDeleted
# 维护人：脚本规范 V1（2026-09-24）
# ============================================================
set -euo pipefail

[ $# -eq 2 ] || { echo "用法: $0 <DictCode> <ItemCode>" >&2; exit 2; }
DICT_CODE="$1"
ITEM_CODE="$2"

HERE="$(cd "$(dirname "$0")" && pwd)"
SQL_FILE="$HERE/verify_dict_softdelete.sql"
[ -f "$SQL_FILE" ] || { echo "✗ 缺少 SQL 文件：${SQL_FILE}" >&2; exit 1; }

DB="${YZH_DB_NAME:-yzh_cert_platform}"
MYSQL_PWD_ROOT="${MYSQL_PWD_ROOT:-Yzh123456.}"

# SQL 外置（铁律 B3）：脚本只做「参数注入 + 喂给 mysql」，不持有任何 SQL 文本。
sed -e "s|__DICT_CODE__|${DICT_CODE}|g" -e "s|__ITEM_CODE__|${ITEM_CODE}|g" "$SQL_FILE" \
  | docker exec -i -e MYSQL_PWD="$MYSQL_PWD_ROOT" yzh-mysql mysql -uroot -N -B "$DB"
