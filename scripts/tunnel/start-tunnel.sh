#!/bin/bash
#
# start-tunnel.sh — 一键把本地前端 dev server 通过 Cloudflare 临时隧道暴露到公网
#
# 【作用】
#   为 cert-admin(9990) / cert-auditor(9991) 各开一条 Cloudflare Quick Tunnel
#   （*.trycloudflare.com，免域名、免注册、免登录）。
#
#   外网访问链路（关键）：
#     浏览器 --HTTPS--> Cloudflare 边缘 --隧道--> 本机 Vite dev server
#       --Vite proxy(/api)--> 后端 127.0.0.1:9992 --SDK--> MySQL/Redis/MinIO
#
#   因此后端 9992 与数据层（MySQL 3307 / Redis 6380 / MinIO 9000）无需
#   也不允许对外暴露：前端用相对路径 /api 调用，由 Vite proxy 在本机转发。
#
# 【用法】
#   ./scripts/tunnel/start-tunnel.sh              # 两个前端都开（默认）
#   ./scripts/tunnel/start-tunnel.sh admin        # 只开 admin(9990)
#   ./scripts/tunnel/start-tunnel.sh auditor      # 只开 auditor(9991)
#
#   关闭：./scripts/tunnel/stop-tunnel.sh
#   查看：./scripts/tunnel/status-tunnel.sh
#
# 【依赖】
#   cloudflared（缺失时自动下载到 scripts/tunnel/bin/，约 20MB）
#   curl / lsof / tar（macOS 自带）
#
# 【前置】
#   admin / auditor 前端已在运行（scripts/frontend/start.sh admin start）
#   vite.config.ts 已配置 server.allowedHosts（Vite >= 5.4.12 否则外网 403）
#
# 【维护人】wangqingquan
#
# 【安全约束】
#   · 只建立「本机 -> Cloudflare 边缘」的出站隧道；不修改任何系统配置、
#     不把本机端口开放到局域网或公网。
#   · 隧道为随机 URL，进程退出即失效，随时可用 stop-tunnel.sh 关闭。
#   · 严禁在本脚本内加入暴露 9992 / 3307 / 6380 / 9000 的逻辑。
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
BIN_DIR="$SCRIPT_DIR/bin"
RUN_DIR="$SCRIPT_DIR/run"
URL_FILE="$RUN_DIR/tunnel-urls.txt"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
# 注意：日志一律写 stderr。stdout 只用于 start_one 的返回值（name|port|url），
# 否则 $(start_one ...) 会把日志行一并捕获，导致 URL 解析错位。
info()  { echo -e "${GREEN}[INFO]${NC} $1" >&2; }
warn()  { echo -e "${YELLOW}[WARN]${NC} $1" >&2; }
error() { echo -e "${RED}[ERROR]${NC} $1" >&2; }

mkdir -p "$RUN_DIR"

# 各端 dev 端口（与 scripts/README.md §2.3 保持一致）
port_of() {
  case "$1" in
    admin)   echo 9990 ;;
    auditor) echo 9991 ;;
    *)       echo "" ;;
  esac
}

# ---------------- 参数解析 ----------------
TARGET="${1:-all}"
case "$TARGET" in
  all)           SERVICES="admin auditor" ;;
  admin|auditor) SERVICES="$TARGET" ;;
  *)             error "未知目标: ${TARGET}（可选 admin|auditor|all）"; exit 1 ;;
esac

# ---------------- 1. 准备 cloudflared ----------------
resolve_cloudflared() {
  if command -v cloudflared >/dev/null 2>&1; then
    command -v cloudflared
    return 0
  fi
  if [ -x "$BIN_DIR/cloudflared" ]; then
    echo "$BIN_DIR/cloudflared"
    return 0
  fi
  return 1
}

download_cloudflared() {
  local arch asset url tmp
  arch="$(uname -m)"
  case "$arch" in
    arm64|aarch64) asset="cloudflared-darwin-arm64.tgz" ;;
    x86_64)        asset="cloudflared-darwin-amd64.tgz" ;;
    *) error "不支持的 CPU 架构: $arch"; exit 1 ;;
  esac
  url="https://github.com/cloudflare/cloudflared/releases/latest/download/$asset"
  info "未检测到 cloudflared，正在下载（${asset}）..."
  mkdir -p "$BIN_DIR"
  tmp="$(mktemp -d)"
  if ! curl -fL --connect-timeout 15 --progress-bar "$url" -o "$tmp/cf.tgz"; then
    rm -rf "$tmp"
    error "下载失败（GitHub 不可达）。可手动下载后放到: $BIN_DIR/cloudflared"
    error "地址: $url"
    exit 1
  fi
  tar -xzf "$tmp/cf.tgz" -C "$tmp"
  mv "$tmp/cloudflared" "$BIN_DIR/cloudflared"
  chmod +x "$BIN_DIR/cloudflared"
  rm -rf "$tmp"
  info "cloudflared 已安装: $BIN_DIR/cloudflared"
}

CF_BIN="$(resolve_cloudflared || true)"
if [ -z "$CF_BIN" ]; then
  download_cloudflared
  CF_BIN="$BIN_DIR/cloudflared"
fi
# 去掉 macOS 下载隔离属性，避免被 Gatekeeper 拦截
if [ -x "$CF_BIN" ]; then
  xattr -d com.apple.quarantine "$CF_BIN" 2>/dev/null || true
fi
info "cloudflared: $CF_BIN ($("$CF_BIN" --version 2>/dev/null | head -1 || echo 'unknown'))"

# ---------------- 2. 前置检查 ----------------
check_port() {
  local port="$1" name="$2"
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN -t >/dev/null 2>&1; then
    return 0
  fi
  error "$name 前端未运行（端口 $port 无监听）"
  error "请先启动: $PROJECT_DIR/scripts/frontend/start.sh $name start"
  return 1
}

check_allowed_hosts() {
  local name="$1" cfg="$PROJECT_DIR/src/certplatform-web/cert/cert-$1/vite.config.ts"
  if [ -f "$cfg" ] && ! grep -q "allowedHosts" "$cfg"; then
    warn "$name 的 vite.config.ts 缺少 allowedHosts，外网访问会被 Vite 拒为 403"
    warn "且修改 vite.config.ts 后必须重启该前端才生效"
  fi
}

# ---------------- 3. 启动单条隧道 ----------------
start_one() {
  local name="$1" port log pidfile old_url url i
  port="$(port_of "$name")"
  log="$RUN_DIR/${name}.log"
  pidfile="$RUN_DIR/${name}.pid"

  # 已在运行则不重复启动
  if [ -f "$pidfile" ] && kill -0 "$(cat "$pidfile")" 2>/dev/null; then
    old_url="$(grep -oE 'https://[A-Za-z0-9-]+\.trycloudflare\.com' "$log" 2>/dev/null | head -1 || true)"
    warn "$name 隧道已在运行（PID $(cat "$pidfile")）"
    echo "${name}|${port}|${old_url}"
    return 0
  fi

  check_port "$port" "$name" || return 1
  check_allowed_hosts "$name"

  info "正在建立 $name 隧道（127.0.0.1:$port -> Cloudflare 边缘）..."
  : > "$log"
  nohup "$CF_BIN" tunnel --url "http://127.0.0.1:$port" --no-autoupdate \
        >"$log" 2>&1 &
  echo $! > "$pidfile"

  url=""
  for i in $(seq 1 45); do
    url="$(grep -oE 'https://[A-Za-z0-9-]+\.trycloudflare\.com' "$log" 2>/dev/null | head -1 || true)"
    [ -n "$url" ] && break
    if ! kill -0 "$(cat "$pidfile")" 2>/dev/null; then
      error "$name 隧道进程异常退出，日志尾部："
      tail -15 "$log"
      return 1
    fi
    sleep 1
  done

  if [ -z "$url" ]; then
    error "$name 隧道 45s 内未取得公网地址，详见日志: $log"
    return 1
  fi

  info "$name 隧道就绪（耗时 ${i}s）"
  echo "${name}|${port}|${url}"
}

# ---------------- 4. 执行 ----------------
RESULTS=()
for s in $SERVICES; do
  if r="$(start_one "$s")"; then
    RESULTS+=("$r")
  fi
done

if [ ${#RESULTS[@]} -eq 0 ]; then
  error "没有成功建立任何隧道"
  exit 1
fi

# 写入地址文件（供其他脚本/人工查阅）
{
  echo "# Cloudflare 临时隧道地址"
  echo "# 生成时间: $(date '+%Y-%m-%d %H:%M:%S')"
  echo "# 注意: Quick Tunnel 地址为随机值，每次重启都会变化"
  for r in "${RESULTS[@]}"; do
    IFS='|' read -r n p u <<< "$r"
    [ -n "$p" ] || continue
    echo "$n  (本机 127.0.0.1:$p)  ->  $u"
  done
} > "$URL_FILE"

# ---------------- 5. 结果输出 ----------------
echo ""
echo "=========================================================="
echo "  公网访问地址（临时，进程退出/重启即失效）"
echo "=========================================================="
for r in "${RESULTS[@]}"; do
  IFS='|' read -r n p u <<< "$r"
  case "$n" in
    admin)   label="管理端 cert-admin  " ;;
    auditor) label="审核端 cert-auditor" ;;
    *)       label="$n" ;;
  esac
  echo "  $label  $u"
done
echo "----------------------------------------------------------"
echo "  未暴露（外网不可直达，也无需直达）："
echo "    后端 API  127.0.0.1:9992   <- 由前端 Vite proxy 在本机转发 /api"
echo "    MySQL     127.0.0.1:3307   <- 后端经 SDK 访问，仅本机出站"
echo "    Redis     127.0.0.1:6380   <- 同上"
echo "    MinIO     127.0.0.1:9000   <- 文件走后端流式代理，无预签名 URL"
echo "----------------------------------------------------------"
echo "  地址清单: $URL_FILE"
echo "  日志目录: $RUN_DIR"
echo "  关闭隧道: $SCRIPT_DIR/stop-tunnel.sh"
echo "=========================================================="
echo ""
warn "首次对外演示前请确认：① 使用测试数据 ② 首页能正常登录 ③ 不对外透露账号口令"
