#!/usr/bin/env bash
###############################################################################
# import-surveyresult.sh — 把「测绘成果管理系统」真实库 dump 还原到本地 docker MySQL
#
# 源：SQLyog 导出的 MySQL 5.7 dump（含 CREATE DATABASE / CREATE TABLE / INSERT / TRIGGER）
# 目标：容器 yzh-mysql（MySQL 8.0.46，宿主端口 3307）
#
# 用法：
#   ./import-surveyresult.sh                 # 库不存在则导入；已存在则中止（防误覆盖）
#   ./import-surveyresult.sh --recreate      # 先 DROP 再导入
#   ./import-surveyresult.sh --verify-only   # 只做导入后体检
#
# 环境变量可覆盖：DUMP / DB / APP_USER / APP_PASS / CONTAINER / ROOT_PASS
###############################################################################
set -uo pipefail

CONTAINER="${CONTAINER:-yzh-mysql}"
ROOT_USER="${ROOT_USER:-root}"
ROOT_PASS="${ROOT_PASS:-Yzh123456.}"
DB="${DB:-surveyresult}"
APP_USER="${APP_USER:-surveyresult}"
APP_PASS="${APP_PASS:-vip2015lbzc}"
DUMP="${DUMP:-/Volumes/Expand/wangqingquan/Library/Containers/com.tencent.xinWeChat/Data/Documents/xwechat_files/wxid_18g89lqst07m21_55ec/msg/file/2026-10/bchsjk.sql}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="$SCRIPT_DIR/logs"
mkdir -p "$LOG_DIR"
TS="$(date +%Y%m%d_%H%M%S)"
IMPORT_LOG="$LOG_DIR/import_${DB}_${TS}.log"
ERROR_LOG="$LOG_DIR/import_${DB}_${TS}.err"
SUMMARY_LOG="$LOG_DIR/verify_${DB}_${TS}.log"

MODE="import"
for arg in "$@"; do
  case "$arg" in
    --recreate)    MODE="recreate" ;;
    --verify-only) MODE="verify" ;;
    -h|--help)     sed -n '2,20p' "$0"; exit 0 ;;
    *) echo "未知参数: $arg"; exit 2 ;;
  esac
done

c_ok()   { printf '\033[32m✔\033[0m %s\n' "$*"; }
c_warn() { printf '\033[33m!\033[0m %s\n' "$*"; }
c_err()  { printf '\033[31m✘\033[0m %s\n' "$*"; }
c_step() { printf '\n\033[36m== %s ==\033[0m\n' "$*"; }

# root 会话（MYSQL_PWD 避免命令行密码告警）
mysql_root() {
  docker exec -i -e MYSQL_PWD="$ROOT_PASS" "$CONTAINER" \
    mysql -u"$ROOT_USER" --default-character-set=utf8mb4 "$@"
}

# 业务账号会话（验证账号可用性）
mysql_app() {
  docker exec -i -e MYSQL_PWD="$APP_PASS" "$CONTAINER" \
    mysql -u"$APP_USER" --default-character-set=utf8mb4 "$@"
}

###############################################################################
# 0. 前置检查
###############################################################################
c_step "0/5 前置检查"

if [ "$(docker inspect -f '{{.State.Running}}' "$CONTAINER" 2>/dev/null)" != "true" ]; then
  c_err "容器 $CONTAINER 未运行。先执行: cd docker && ./start.sh"
  exit 1
fi
c_ok "容器 $CONTAINER 运行中"

if ! mysql_root -N -e "SELECT 1" >/dev/null 2>&1; then
  c_err "root 连接失败（密码错误？）"
  exit 1
fi
c_ok "root 连接正常（MySQL $(mysql_root -N -e 'SELECT VERSION()')）"

if [ "$MODE" != "verify" ]; then
  if [ ! -f "$DUMP" ]; then
    c_err "dump 文件不存在: $DUMP"
    exit 1
  fi
  DUMP_SIZE=$(stat -f%z "$DUMP" 2>/dev/null || stat -c%s "$DUMP")
  c_ok "dump 文件存在: $(awk -v s="$DUMP_SIZE" 'BEGIN{printf "%.2f GB", s/1073741824}')"
fi

###############################################################################
# 1. 库与账号
###############################################################################
c_step "1/5 目标库与账号"

DB_EXISTS=$(mysql_root -N -e "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='$DB'" 2>/dev/null | tr -d '[:space:]')

if [ "$MODE" = "verify" ]; then
  [ "$DB_EXISTS" = "1" ] || { c_err "库 $DB 不存在，无法体检"; exit 1; }
else
  if [ "$DB_EXISTS" = "1" ]; then
    if [ "$MODE" = "recreate" ]; then
      c_warn "库 $DB 已存在 → --recreate 指定，执行 DROP"
      mysql_root -e "DROP DATABASE \`$DB\`;" || exit 1
    else
      c_err "库 $DB 已存在。要覆盖请加 --recreate"
      exit 1
    fi
  fi
  # dump 内部会执行 CREATE DATABASE IF NOT EXISTS（utf8 / utf8_unicode_ci），此处只保证库存在
  mysql_root -e "CREATE DATABASE IF NOT EXISTS \`$DB\` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;" || exit 1
  c_ok "库 $DB 就绪"

  # 专用账号：兼容 MySQL 5.7 时代客户端（Navicat 旧版不支持 caching_sha2_password）
  mysql_root -e "
    CREATE USER IF NOT EXISTS '$APP_USER'@'%' IDENTIFIED WITH mysql_native_password BY '$APP_PASS';
    ALTER USER '$APP_USER'@'%' IDENTIFIED WITH mysql_native_password BY '$APP_PASS';
    GRANT ALL PRIVILEGES ON \`$DB\`.* TO '$APP_USER'@'%';
    FLUSH PRIVILEGES;" || exit 1
  c_ok "账号 $APP_USER / $APP_PASS 已授权 $DB.*"
fi

###############################################################################
# 2. 导入（临时调优，结束必恢复）
###############################################################################
restore_globals() {
  mysql_root -e "SET GLOBAL innodb_flush_log_at_trx_commit=1; SET GLOBAL max_allowed_packet=67108864;" >/dev/null 2>&1
}
trap restore_globals EXIT INT TERM

if [ "$MODE" != "verify" ]; then
  c_step "2/5 导入中（2 GB，预计 5–20 分钟，勿中断）"
  echo "  dump : $DUMP"
  echo "  log  : $IMPORT_LOG"
  echo "  err  : $ERROR_LOG"

  # 全局提速：崩溃最多丢 1 秒事务，导入窗口内可接受；trap 负责恢复
  mysql_root -e "SET GLOBAL innodb_flush_log_at_trx_commit=2; SET GLOBAL max_allowed_packet=268435456;" >/dev/null 2>&1

  START_TS=$(date +%s)
  # sql_log_bin=0：不写 binlog，2 GB 导入显著提速并省空间
  docker exec -i -e MYSQL_PWD="$ROOT_PASS" "$CONTAINER" \
    mysql -u"$ROOT_USER" \
      --default-character-set=utf8mb4 \
      --max-allowed-packet=256M \
      --init-command="SET sql_log_bin=0" \
      --show-warnings=OFF \
    < "$DUMP" > "$IMPORT_LOG" 2> "$ERROR_LOG"
  RC=$?
  END_TS=$(date +%s)

  if [ $RC -ne 0 ]; then
    c_err "导入进程返回码 ${RC}（耗时 $((END_TS-START_TS))s）"
    echo "--- 错误日志尾部 ---"
    tail -40 "$ERROR_LOG"
    exit 1
  fi
  c_ok "导入完成，耗时 $((END_TS-START_TS))s"

  if [ -s "$ERROR_LOG" ]; then
    c_warn "stderr 非空（多为 8.0 弃用告警），前 10 行："
    head -10 "$ERROR_LOG"
  fi
fi

###############################################################################
# 2.5 触发器 DEFINER 修正
#     原库 DEFINER 为业务账号（如 yczhzj@%），本地不存在 ⇒ 触发时报 ERROR 1449。
#     统一重建为 root@localhost（仅本地，权限受控）。
###############################################################################
fix_trigger_definer() {
  local bad
  bad=$(mysql_root -N -e "
    SELECT COUNT(*) FROM information_schema.TRIGGERS t
    LEFT JOIN mysql.user u ON CONCAT(u.User,'@',u.Host)=t.DEFINER
    WHERE t.TRIGGER_SCHEMA='$DB' AND u.User IS NULL;" | tr -d '[:space:]')

  if [ "${bad:-0}" = "0" ]; then
    c_ok "触发器 DEFINER 均有效"
    return 0
  fi

  c_warn "$bad 个触发器 DEFINER 账号不存在，重建为 root@localhost"
  docker exec -e MYSQL_PWD="$ROOT_PASS" "$CONTAINER" \
      mysqldump -u"$ROOT_USER" --triggers --no-data --no-create-info \
                --skip-add-drop-table --skip-comments "$DB" 2>/dev/null \
    | sed -E 's/DEFINER=`[^`]+`@`[^`]+`/DEFINER=`root`@`localhost`/g' \
    | docker exec -i -e MYSQL_PWD="$ROOT_PASS" "$CONTAINER" \
        mysql -u"$ROOT_USER" --default-character-set=utf8mb4 "$DB" 2>&1 \
    | grep -v "Trigger does not exist" || true

  local still
  still=$(mysql_root -N -e "
    SELECT COUNT(*) FROM information_schema.TRIGGERS t
    LEFT JOIN mysql.user u ON CONCAT(u.User,'@',u.Host)=t.DEFINER
    WHERE t.TRIGGER_SCHEMA='$DB' AND u.User IS NULL;" | tr -d '[:space:]')
  if [ "${still:-0}" = "0" ]; then
    c_ok "触发器 DEFINER 已修正"
  else
    c_warn "仍有 $still 个触发器 DEFINER 无效，请手工检查"
  fi
}

if [ "$MODE" != "verify" ]; then
  c_step "2.5/5 触发器 DEFINER 修正"
  fix_trigger_definer
fi

###############################################################################
# 3. 体检
###############################################################################
c_step "3/5 结构体检"

{
  echo "===== $DB 体检 @ $(date '+%F %T') ====="
  echo
  echo "--- 对象计数 ---"
  mysql_root -t -e "
    SELECT
      (SELECT COUNT(*) FROM information_schema.TABLES
        WHERE TABLE_SCHEMA='$DB' AND TABLE_TYPE='BASE TABLE')  AS '基表',
      (SELECT COUNT(*) FROM information_schema.VIEWS
        WHERE TABLE_SCHEMA='$DB')                              AS '视图',
      (SELECT COUNT(*) FROM information_schema.TRIGGERS
        WHERE TRIGGER_SCHEMA='$DB')                            AS '触发器',
      (SELECT COUNT(*) FROM information_schema.ROUTINES
        WHERE ROUTINE_SCHEMA='$DB')                            AS '存储过程/函数',
      (SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS
        WHERE CONSTRAINT_SCHEMA='$DB')                         AS '外键';"

  echo
  echo "--- 数据量 ---"
  mysql_root -t -e "
    SELECT
      COUNT(*)                                                   AS '表数',
      ROUND(SUM(DATA_LENGTH)/1048576, 1)                         AS '数据MB',
      ROUND(SUM(INDEX_LENGTH)/1048576, 1)                        AS '索引MB',
      ROUND(SUM(DATA_LENGTH+INDEX_LENGTH)/1048576, 1)            AS '合计MB',
      SUM(TABLE_ROWS)                                            AS '估算行数'
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA='$DB' AND TABLE_TYPE='BASE TABLE';"

  echo
  echo "--- 字符集分布 ---"
  mysql_root -t -e "
    SELECT TABLE_COLLATION, COUNT(*) AS 表数
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA='$DB' AND TABLE_TYPE='BASE TABLE'
    GROUP BY TABLE_COLLATION ORDER BY 表数 DESC;"

  echo
  echo "--- 精确行数（前 25 大表）---"
  TOP_TABLES=$(mysql_root -N -e "
    SELECT TABLE_NAME FROM information_schema.TABLES
    WHERE TABLE_SCHEMA='$DB' AND TABLE_TYPE='BASE TABLE'
    ORDER BY DATA_LENGTH+INDEX_LENGTH DESC LIMIT 25;")
  UNION_SQL=""
  for t in $TOP_TABLES; do
    [ -n "$UNION_SQL" ] && UNION_SQL="$UNION_SQL UNION ALL "
    UNION_SQL="$UNION_SQL SELECT '$t' AS tbl, COUNT(*) AS rows_cnt FROM \`$t\`"
  done
  if [ -n "$UNION_SQL" ]; then
    mysql_root -t -D "$DB" -e "SELECT tbl AS '表', rows_cnt AS '行数' FROM ($UNION_SQL) x ORDER BY rows_cnt DESC;"
  fi
} 2>&1 | tee "$SUMMARY_LOG"

###############################################################################
# 4. 账号连通性
###############################################################################
c_step "4/5 业务账号连通性"
if mysql_app -N -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='$DB'" >/dev/null 2>&1; then
  OBJ_CNT=$(mysql_app -N -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='${DB}'")
  c_ok "账号 ${APP_USER} 可读 ${DB}（${OBJ_CNT} 个对象）"
else
  c_err "账号 $APP_USER 连接失败"
fi

###############################################################################
# 5. 收尾
###############################################################################
c_step "5/5 完成"
cat <<EOF
连接信息（宿主）：
  host=127.0.0.1  port=3307  db=$DB
  user=$APP_USER  password=$APP_PASS
  （root 仍为 ${ROOT_USER} / ${ROOT_PASS}，未改动）

体检报告：$SUMMARY_LOG
EOF
