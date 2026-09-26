#!/bin/bash
# ============================================================
# 企业机构树收敛 · 修复（★ 会写库，必须显式 --apply）
#
# 作用：把历史遗留的 Sys_Organization 企业节点收敛到统一形态
#         虚拟体系机构(L1) → 专家注册人员(L2) → 企业信息(L3) → 具体企业(L4)
#       并软删「幽灵节点」（企业已删但节点还在）与「重复节点」。
#
# 用法：./fix-enterprise-org-tree-2026-09-26.sh           # 只跑门禁+诊断，不写库
#       ./fix-enterprise-org-tree-2026-09-26.sh --apply   # 备份后执行修复
#       ⚠️ 若无执行位（新脚本默认 644），用 `bash <路径> [--apply]` 调用即可
#
# 依赖：docker 容器 yzh-mysql（YZH_MYSQL_CONTAINER 覆盖）
#       ../verify/enterprise-org-gate.sql        （机器门禁，单行计数）
#       ../verify/verify-enterprise-org-tree.sql （人读诊断）
#       ./fix-enterprise-org-tree-2026-09-26.sql （实际修复 SQL）
#
# 退出码：0 成功｜1 环境/前置失败｜2 用法错误｜3 门禁拒绝
# 维护人：脚本规范 V1（2026-09-26）
#
# ⛔ 安全约束（scripts/README.md 铁律 B6 · 四道自锁，全部通过才写库）
#   ① 库名白名单：仅允许 yzh_cert_platform 或 yzh_cert_platform_*（YZH_DB_NAME 覆盖）
#   ② 显式开关：无 --apply 只打印诊断，**绝不写库**
#   ③ 前置门禁：PeopleOnEnterpriseNodes > 0 → 拒绝执行
#      （软删企业节点后，挂在其上的人员 OrgCode 会指向不存在的节点）
#   ④ 自动备份：写库前 mysqldump Sys_Organization + cert_enterprise 到 ../backup/
#
# ⚠️ 职责边界（铁律 B1–B7）：本脚本是**编排层**，SQL 全部外置（B3），
#    本文件内**不出现任何 SQL 语句**。
# ⚠️ 可逆：修复只做软删（IsDeleted=1），改回 0 / IsValid 改回 1 即恢复。
# ============================================================
set -euo pipefail

# ------------------------------------------------------------
# 参数解析（铁律 B6 闸②：无 --apply 不写库）
# ------------------------------------------------------------
APPLY=0
for arg in "$@"; do
  case "$arg" in
    --apply)   APPLY=1 ;;
    -h|--help) sed -n '2,30p' "$0"; exit 0 ;;
    *) echo "✗ 未知参数：${arg}（用法：$0 [--apply]）" >&2; exit 2 ;;
  esac
done

HERE="$(cd "$(dirname "$0")" && pwd)"
VERIFY_DIR="$(cd "$HERE/../verify" && pwd)"
GATE_SQL="$VERIFY_DIR/enterprise-org-gate.sql"
DIAG_SQL="$VERIFY_DIR/verify-enterprise-org-tree.sql"
FIX_SQL="$HERE/fix-enterprise-org-tree-2026-09-26.sql"
BACKUP_DIR="$(cd "$HERE/.." && pwd)/backup"

for f in "$GATE_SQL" "$DIAG_SQL" "$FIX_SQL"; do
  [ -f "$f" ] || { echo "✗ 缺少文件：${f}" >&2; exit 1; }
done
command -v docker >/dev/null 2>&1 || { echo "✗ 未找到 docker 命令" >&2; exit 1; }

DB="${YZH_DB_NAME:-yzh_cert_platform}"
MYSQL_PWD_ROOT="${MYSQL_PWD_ROOT:-Yzh123456.}"
MYSQL_CONTAINER="${YZH_MYSQL_CONTAINER:-yzh-mysql}"
MYSQL=(docker exec -i -e MYSQL_PWD="$MYSQL_PWD_ROOT" "$MYSQL_CONTAINER" mysql -uroot)

# ------------------------------------------------------------
# 铁律 B6 闸①：库名白名单（防止误连生产/他项目库）
# ------------------------------------------------------------
case "$DB" in
  yzh_cert_platform|yzh_cert_platform_*) ;;
  *) echo "⛔ 拒绝执行：库名『${DB}』不在白名单（yzh_cert_platform / yzh_cert_platform_*）" >&2
     echo "   如需对测试库执行，请显式：YZH_DB_NAME=yzh_cert_platform_test $0 --apply" >&2
     exit 3 ;;
esac

echo "═══════════════════════════════════════════════════════════"
echo "  企业机构树收敛 · 库=${DB} · 模式=$([ "$APPLY" = 1 ] && echo '执行(--apply)' || echo '演练(只读)')"
echo "═══════════════════════════════════════════════════════════"

# ------------------------------------------------------------
# 铁律 B6 闸③：前置门禁（机器判定）
# ------------------------------------------------------------
gate_row="$("${MYSQL[@]}" -N -B -r --default-character-set=utf8mb4 "$DB" < "$GATE_SQL")"
IFS=$'\t' read -r PEOPLE GHOST DUP MISSING LEGACY MISPLACED NAMEDESYNC VALIDDESYNC <<< "$gate_row" || true
[ -n "${PEOPLE:-}" ] || { echo "✗ 门禁 SQL 未返回可解析结果：『${gate_row}』" >&2; exit 1; }

# ⚠️ 不用 `printf '%-30s'` 对齐：bash 的 printf 按**字节**计算宽度，
#    中文标签（3 字节/字）必然错位。改用「标签：值」定长前缀。
echo ""
echo "── 门禁计数（只读）────────────────────────────────────────"
echo "  ★ 挂在企业节点上的人员      ：${PEOPLE}"
echo "    幽灵节点                  ：${GHOST}"
echo "    重复节点组                ：${DUP}"
echo "    缺「企业信息」文件夹的工作区：${MISSING}"
echo "    仍叫「企业用户」的旧文件夹  ：${LEGACY}"
echo "    企业节点挂错层            ：${MISPLACED}"
echo "    节点名与企业名不一致      ：${NAMEDESYNC}"
echo "    节点状态与企业状态不一致  ：${VALIDDESYNC}"

if [ "$APPLY" = 0 ]; then
  echo ""
  echo "✓ 演练结束（未修改任何数据）。"
  echo "  · 详细诊断：./../verify/verify-enterprise-org-tree.sh"
  echo "  · 确认无误后执行：$0 --apply"
  exit 0
fi

if [ "$PEOPLE" != "0" ]; then
  echo "" >&2
  echo "⛔ 拒绝执行：有 ${PEOPLE} 名人员挂在企业节点上。" >&2
  echo "   软删这些节点后，他们的 OrgCode 会指向不存在的节点。" >&2
  echo "   请先决定这些人员改挂到哪个分组（通常是工作区下的「管理员」），" >&2
  echo "   用「机构-人员管理」调整后再执行本脚本。" >&2
  exit 3
fi

# ------------------------------------------------------------
# 铁律 B6 闸④：自动备份（写库前）
# ------------------------------------------------------------
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d_%H%M%S)"
BACKUP_FILE="$BACKUP_DIR/org_tree_before_fix_${STAMP}.sql"

docker exec -e MYSQL_PWD="$MYSQL_PWD_ROOT" "$MYSQL_CONTAINER" \
  mysqldump -uroot --default-character-set=utf8mb4 --single-transaction \
  "$DB" Sys_Organization cert_enterprise > "$BACKUP_FILE"

if [ ! -s "$BACKUP_FILE" ]; then
  echo "✗ 备份失败（文件为空）：${BACKUP_FILE} → 拒绝继续执行" >&2
  exit 1
fi
echo ""
echo "✓ 已备份：${BACKUP_FILE}"

# ------------------------------------------------------------
# 执行修复（SQL 外置，铁律 B3）
# ------------------------------------------------------------
echo ""
echo "── 执行修复 SQL ───────────────────────────────────────────"
"${MYSQL[@]}" -t -r --default-character-set=utf8mb4 "$DB" < "$FIX_SQL"

# ------------------------------------------------------------
# 执行后复核（B7：失败要响）
# ------------------------------------------------------------
after_row="$("${MYSQL[@]}" -N -B -r --default-character-set=utf8mb4 "$DB" < "$GATE_SQL")"
IFS=$'\t' read -r A_PEOPLE A_GHOST A_DUP A_MISSING A_LEGACY A_MISPLACED A_NAMEDESYNC A_VALIDDESYNC <<< "$after_row" || true

echo ""
echo "── 修复后复核（前 → 后）──────────────────────────────────"
echo "  幽灵节点                ：${GHOST} → ${A_GHOST}"
echo "  重复节点组              ：${DUP} → ${A_DUP}"
echo "  缺文件夹的工作区        ：${MISSING} → ${A_MISSING}"
echo "  旧名「企业用户」文件夹  ：${LEGACY} → ${A_LEGACY}"
echo "  企业节点挂错层          ：${MISPLACED} → ${A_MISPLACED}"
echo "  节点名未同步            ：${NAMEDESYNC} → ${A_NAMEDESYNC}"
echo "  节点状态未同步          ：${VALIDDESYNC} → ${A_VALIDDESYNC}"

FAIL=0
# ★ 复核含「挂在企业节点上的人员」—— 修复本不该碰人员，非 0 说明出了意外，必须报警
for pair in "挂人:$A_PEOPLE" "幽灵节点:$A_GHOST" "重复节点组:$A_DUP" "缺文件夹:$A_MISSING" "旧名文件夹:$A_LEGACY" "挂错层:$A_MISPLACED" "名称未同步:$A_NAMEDESYNC" "状态未同步:$A_VALIDDESYNC"; do
  name="${pair%%:*}"; val="${pair##*:}"
  if [ "$val" != "0" ]; then
    echo "  ⚠️ ${name} 复核未归零：${val}" >&2
    FAIL=1
  fi
done

echo ""
if [ "$FAIL" = 0 ]; then
  echo "✓ 修复完成，全部复核指标已归零。"
  echo "  回滚（如需）：docker exec -i yzh-mysql mysql -uroot -p*** ${DB} < ${BACKUP_FILE}"
else
  echo "⚠️ 修复已执行，但仍有指标未归零 —— 请跑 ./../verify/verify-enterprise-org-tree.sh 人读排查。" >&2
  echo "  回滚：docker exec -i yzh-mysql mysql -uroot -p*** ${DB} < ${BACKUP_FILE}" >&2
  exit 1
fi
