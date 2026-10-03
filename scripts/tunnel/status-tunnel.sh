#!/bin/bash
#
# status-tunnel.sh — 查看 Cloudflare 临时隧道运行状态
#
# 【作用】列出 admin/auditor 隧道的进程状态、本机端口与当前公网地址。
# 【用法】./scripts/tunnel/status-tunnel.sh
# 【依赖】lsof
# 【维护人】wangqingquan
# 【安全约束】只读，无任何写操作或副作用。
#
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RUN_DIR="$SCRIPT_DIR/run"

echo "=========================================================="
echo "  Cloudflare 临时隧道状态"
echo "=========================================================="

for s in admin auditor; do
  case "$s" in
    admin)   port=9990 ;;
    auditor) port=9991 ;;
  esac

  log="$RUN_DIR/$s.log"
  pidfile="$RUN_DIR/$s.pid"

  # 本机前端
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN -t >/dev/null 2>&1; then
    local_state="运行中"
  else
    local_state="未运行"
  fi

  # 隧道进程
  if [ -f "$pidfile" ] && kill -0 "$(cat "$pidfile")" 2>/dev/null; then
    tstate="已开启 (PID $(cat "$pidfile"))"
    url="$(grep -oE 'https://[A-Za-z0-9-]+\.trycloudflare\.com' "$log" 2>/dev/null | head -1 || echo '(未解析到地址)')"
  else
    tstate="未开启"
    url="-"
  fi

  echo ""
  echo "  [$s]  本机端口 $port : $local_state"
  echo "        隧道状态 : $tstate"
  echo "        公网地址 : $url"
done

echo ""
if [ -f "$RUN_DIR/tunnel-urls.txt" ]; then
  echo "  地址清单文件: $RUN_DIR/tunnel-urls.txt"
fi
echo "=========================================================="
