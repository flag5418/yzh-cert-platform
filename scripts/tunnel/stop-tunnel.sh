#!/bin/bash
#
# stop-tunnel.sh — 关闭由 start-tunnel.sh 建立的 Cloudflare 临时隧道
#
# 【作用】按 run/*.pid 关闭隧道进程并清理运行态文件，公网地址随即失效。
# 【用法】./scripts/tunnel/stop-tunnel.sh           # 关闭全部
#         ./scripts/tunnel/stop-tunnel.sh admin     # 只关 admin
#         ./scripts/tunnel/stop-tunnel.sh auditor   # 只关 auditor
# 【依赖】无
# 【维护人】wangqingquan
# 【安全约束】只 kill 本脚本目录 run/*.pid 记录的进程，不触碰其他进程。
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RUN_DIR="$SCRIPT_DIR/run"

TARGET="${1:-all}"
case "$TARGET" in
  all)           SERVICES="admin auditor" ;;
  admin|auditor) SERVICES="$TARGET" ;;
  *)             echo "用法: $0 [admin|auditor|all]"; exit 1 ;;
esac

echo "=========================================="
echo "    关闭 Cloudflare 临时隧道"
echo "=========================================="

stopped=0
for s in $SERVICES; do
  pidfile="$RUN_DIR/$s.pid"
  if [ ! -f "$pidfile" ]; then
    echo "  $s: 无运行记录"
    continue
  fi
  pid="$(cat "$pidfile" 2>/dev/null || echo "")"
  if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    sleep 1
    if kill -0 "$pid" 2>/dev/null; then
      kill -9 "$pid" 2>/dev/null || true
    fi
    echo "  $s: 已关闭 (PID $pid)"
    stopped=1
  else
    echo "  $s: 进程已不存在，清理残留记录"
  fi
  rm -f "$pidfile"
done

rm -f "$RUN_DIR/tunnel-urls.txt"

if [ "$stopped" = 1 ]; then
  echo ""
  echo "隧道已关闭，原公网地址即刻失效。"
else
  echo ""
  echo "没有正在运行的隧道。"
fi
