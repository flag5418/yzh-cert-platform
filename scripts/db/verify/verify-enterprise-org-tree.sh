#!/bin/bash
# ============================================================
# 企业机构树一致性 · 只读诊断（★ 只读，随时可跑）
#
# 作用：执行 verify-enterprise-org-tree.sql，回显 7 段诊断：
#       ① 企业表全部行 ② 虚拟体系机构子树 ③ ★一致性诊断
#       ④ 幽灵节点 ⑤ 缺「企业信息」文件夹的工作区
#       ⑥ ⚠️ 挂在企业节点上的人员 ⑦ 重复节点
#
# 用法：./verify-enterprise-org-tree.sh
#       ⚠️ 若无执行位（新脚本默认 644），用 `bash <路径>` 调用即可
# 依赖：docker 容器 yzh-mysql（YZH_MYSQL_CONTAINER 覆盖）
#       同目录 verify-enterprise-org-tree.sql
# 退出码：0 = 诊断已输出；1 = 环境/SQL 执行失败
# 维护人：脚本规范 V1（2026-09-26）
#
# ⚠️ 职责边界（scripts/README.md 铁律 B1–B7）：
#      本脚本是**编排层** —— 只负责「调 mysql、回显、报错退出」。
#      SQL 一律外置在同目录 .sql（铁律 B3，禁止内嵌）。
#      只读（铁律 B4）→ **不会修改任何数据**。
# ⚠️ 退出码 0 **不代表数据没问题** —— 请人读第 3/4/6/7 节，有行即不一致。
# ============================================================
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SQL_FILE="$HERE/verify-enterprise-org-tree.sql"
[ -f "$SQL_FILE" ] || { echo "✗ 缺少 SQL 文件：${SQL_FILE}" >&2; exit 1; }

command -v docker >/dev/null 2>&1 || { echo "✗ 未找到 docker 命令" >&2; exit 1; }

DB="${YZH_DB_NAME:-yzh_cert_platform}"
MYSQL_PWD_ROOT="${MYSQL_PWD_ROOT:-Yzh123456.}"
MYSQL_CONTAINER="${YZH_MYSQL_CONTAINER:-yzh-mysql}"

docker exec -i -e MYSQL_PWD="$MYSQL_PWD_ROOT" "$MYSQL_CONTAINER" \
  mysql -uroot -t -r --default-character-set=utf8mb4 "$DB" \
  < "$SQL_FILE"

echo "───────────────────────────────────────────────────────────"
echo "✓ 诊断输出完毕（库：${DB}）"
echo "  ⚠️ 本命令**只读**，未修改任何数据。"
echo "  · 第 3/4/6/7 节有行 → 存量数据不一致，可用修复脚本收敛"
echo "  · ★ 第 6 节有行 → **先人工决定这些人员改挂到哪里**，再考虑修复"
echo "  修复（仅在确认无误后）：scripts/db/fix/fix-enterprise-org-tree-2026-09-26.sh --apply"
