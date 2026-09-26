#!/bin/bash
# ============================================================
# 删除 cert_enterprise.Status 列 · 迁移（★ 会写库 DDL，必须显式 --apply）
#
# 作用：DROP `cert_enterprise`.`Status`。
#       用户 2026-09-26 裁定：`IsValid`（启用/禁用）与 `Status` 是两个含义，
#       但 `Status` 本身无意义 → 直接删除。
#
# 用法：./drop-cert-enterprise-status-2026-09-26.sh           # 只诊断，不写库
#       ./drop-cert-enterprise-status-2026-09-26.sh --apply   # 备份后执行
#       ⚠️ 若无执行位（新脚本默认 644），用 `bash <路径> [--apply]` 调用即可
#
# 依赖：docker 容器 yzh-mysql（YZH_MYSQL_CONTAINER 覆盖）
#       ./drop-cert-enterprise-status-2026-09-26.sql
#
# 退出码：0 成功｜1 环境/前置失败｜2 用法错误｜3 门禁拒绝
# 维护人：脚本规范 V1（2026-09-26）
#
# ⛔ 安全约束（scripts/README.md 铁律 B6 · 三道自锁）
#   ① 库名白名单：仅允许 yzh_cert_platform 或 yzh_cert_platform_*（YZH_DB_NAME 覆盖）
#   ② 显式开关：无 --apply 只诊断，**绝不写库**
#   ③ 自动备份：写库前 mysqldump cert_enterprise 到 ../backup/
#   另：SQL 文件内部还有一道**依赖守卫**（有视图/存储过程引用 cert_enterprise 则主动报错中止）
#
# ⛔⛔ 执行顺序强制：**必须先编译并重启后端**。
#   运行中的旧程序仍会 INSERT `Status` 列 → DROP 后报 `ERROR 1054 Unknown column 'Status'`。
#
# ⚠️ 职责边界（铁律 B1–B7）：本脚本是**编排层**，SQL 全部外置（B3），
#    本文件内**不出现任何 SQL 语句**。
# ============================================================
set -euo pipefail

# ------------------------------------------------------------
# 参数解析（铁律 B6 闸②：无 --apply 不写库）
# ------------------------------------------------------------
APPLY=0
for arg in "$@"; do
  case "$arg" in
    --apply)   APPLY=1 ;;
    -h|--help) sed -n '2,32p' "$0"; exit 0 ;;
    *) echo "✗ 未知参数：${arg}（用法：$0 [--apply]）" >&2; exit 2 ;;
  esac
done

HERE="$(cd "$(dirname "$0")" && pwd)"
SQL_FILE="$HERE/drop-cert-enterprise-status-2026-09-26.sql"
BACKUP_DIR="$(cd "$HERE/.." && pwd)/backup"

[ -f "$SQL_FILE" ] || { echo "✗ 缺少 SQL 文件：${SQL_FILE}" >&2; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "✗ 未找到 docker 命令" >&2; exit 1; }

DB="${YZH_DB_NAME:-yzh_cert_platform}"
MYSQL_PWD_ROOT="${MYSQL_PWD_ROOT:-Yzh123456.}"
MYSQL_CONTAINER="${YZH_MYSQL_CONTAINER:-yzh-mysql}"
MYSQL=(docker exec -i -e MYSQL_PWD="$MYSQL_PWD_ROOT" "$MYSQL_CONTAINER" mysql -uroot)

# ------------------------------------------------------------
# 铁律 B6 闸①：库名白名单
# ------------------------------------------------------------
case "$DB" in
  yzh_cert_platform|yzh_cert_platform_*) ;;
  *) echo "⛔ 拒绝执行：库名『${DB}』不在白名单（yzh_cert_platform / yzh_cert_platform_*）" >&2
     echo "   如需对测试库执行，请显式：YZH_DB_NAME=yzh_cert_platform_test $0 --apply" >&2
     exit 3 ;;
esac

echo "═══════════════════════════════════════════════════════════"
echo "  删除 cert_enterprise.Status · 库=${DB} · 模式=$([ "$APPLY" = 1 ] && echo '执行(--apply)' || echo '演练(只读)')"
echo "═══════════════════════════════════════════════════════════"

if [ "$APPLY" = 0 ]; then
  echo ""
  echo "演练模式：未执行任何 SQL，仅做环境检查。"
  echo "  · 目标 SQL ：${SQL_FILE}"
  echo "  · 备份目录 ：${BACKUP_DIR}"
  echo "  · ⛔ 执行前请确认**后端已编译并重启**（否则旧程序 INSERT 该列会报 ERROR 1054）"
  echo "  · 确认无误后执行：$0 --apply"
  exit 0
fi

# ------------------------------------------------------------
# 铁律 B6 闸③：自动备份（写库前）
# ------------------------------------------------------------
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d_%H%M%S)"
BACKUP_FILE="$BACKUP_DIR/cert_enterprise_before_drop_status_${STAMP}.sql"

docker exec -e MYSQL_PWD="$MYSQL_PWD_ROOT" "$MYSQL_CONTAINER" \
  mysqldump -uroot --default-character-set=utf8mb4 --single-transaction \
  "$DB" cert_enterprise > "$BACKUP_FILE"

if [ ! -s "$BACKUP_FILE" ]; then
  echo "✗ 备份失败（文件为空）：${BACKUP_FILE} → 拒绝继续执行" >&2
  exit 1
fi
echo ""
echo "✓ 已备份：${BACKUP_FILE}"

# ------------------------------------------------------------
# 执行（SQL 外置，铁律 B3；SQL 内部自带依赖守卫 → B7）
# ------------------------------------------------------------
echo ""
echo "── 执行迁移 SQL ───────────────────────────────────────────"
"${MYSQL[@]}" -t -r --default-character-set=utf8mb4 "$DB" < "$SQL_FILE"

echo ""
echo "✓ 迁移完成。"
echo "  回滚（如需，用备份）：docker exec -i yzh-mysql mysql -uroot -p*** ${DB} < ${BACKUP_FILE}"
echo "  ⚠️ 回滚只恢复数据与列，**不会**恢复代码里的 Status 引用（已同步删除）。"
